using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AvaloniaVS.ViewModels;
using EnvDTE;
using EnvDTE100;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using VSLangProj;
using VSLangProj140;

namespace AvaloniaVS.Infrastructure
{
    public sealed class ProjectInfoService
    {
        static readonly ProjectInfoService Instance = new ProjectInfoService();
        private readonly DTE2 _dte;
        public static IEnumerable<ProjectDescriptor> Projects => Instance._cached;

        private bool _solutionRescanQueued = true;
        private bool _targetPathRescanQueued = true;
        private bool _treeRebuildQueued = true;
        private ProjectItemsEvents _solutionItemEvents;
        private SolutionEvents _solutionEvents;


        class ProjectEntry
        {
            private readonly ProjectInfoService _service;
            private ReferencesEvents _events;
            public Project Project { get; }
            public HashSet<Project> References = new HashSet<Project>();
            public ProjectDescriptor Descriptor { get; }
            public bool RescanQueued { get; private set; }
            public ProjectEntry(ProjectInfoService service,  Project project)
            {
                _service = service;
                Project = project;
                Descriptor = new ProjectDescriptor(project);
                if (project.Object is VSProject vsProject)
                {
                    _events = vsProject.Events.ReferencesEvents;
                    _events.ReferenceAdded += r =>
                    {
                        var p = r.SourceProject;
                        if (p != null)
                            References.Add(p);
                        _service._treeRebuildQueued = true;
                    };
                    _events.ReferenceRemoved += r =>
                    {
                        var p = r.SourceProject;
                        if (p != null)
                            References.Remove(p);
                        _service._treeRebuildQueued = true;
                    };
                    _events.ReferenceChanged += delegate
                    {
                        RescanQueued = true;
                    };
                }
                Rescan();
            }

            public void Rescan()
            {
                References.Clear();
                if (Project?.Object is VSProject vsProject)
                    foreach (Reference r in vsProject.References)
                    {
                        var p = r.SourceProject;
                        if (p != null)
                            References.Add(p);
                    }
                RescanQueued = false;
                _service._treeRebuildQueued = true;
            }

            public bool UpdateTargetPath()
            {
                var n = Project.GetAssemblyPath();
                if (n != Descriptor.TargetAssembly)
                {
                    Descriptor.TargetAssembly = n;
                    return true;
                }
                return false;
            }
        }

        private Dictionary<Project, ProjectEntry> _projects = new Dictionary<Project, ProjectEntry>();
        private List<ProjectDescriptor> _cached = new List<ProjectDescriptor>();

        private ProjectInfoService()
        {
            _dte = (DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SDTE));
            _solutionItemEvents = _dte.Events.SolutionItemsEvents;
            _solutionItemEvents.ItemAdded += delegate { _solutionRescanQueued = true; };
            _solutionItemEvents.ItemRenamed += delegate { _solutionRescanQueued = true; };
            _solutionItemEvents.ItemRemoved += delegate { _solutionRescanQueued = true; };
            _solutionEvents = _dte.Events.SolutionEvents;
            _solutionEvents.Opened += delegate { _solutionRescanQueued = true; };

            new DispatcherTimer() { Interval = new TimeSpan(0, 0, 0, 1), IsEnabled = true }.Tick += OnTick;
            OnTick(null, null);
            AvaloniaBuildEvents.Instance.BuildEnd += delegate
            {
                _targetPathRescanQueued = true;
            };
        }
        
        IEnumerable<Project> Collect(ProjectEntry desc)
        {
            foreach (var r in desc.References)
            {
                yield return r;
                if(_projects.TryGetValue(r, out var found))
                    foreach (var ch in Collect(found))
                        yield return ch;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {

            if (_solutionRescanQueued)
            {
                var dic = new Dictionary<Project, ProjectEntry>();

                foreach(var p in GetProjects(_dte.Solution.Projects.OfType<Project>()))
                {
                    if (_projects.TryGetValue(p, out var existing))
                        dic[p] = existing;
                    else
                        dic[p] = new ProjectEntry(this, p);
                }
                _projects = dic;
                _solutionRescanQueued = false;
                _treeRebuildQueued = true;
            }
            bool targetPathsChanged = false;
            foreach (var p in _projects.Values)
            {
                if (p.RescanQueued)
                    p.Rescan();
                if (_targetPathRescanQueued)
                    targetPathsChanged |= p.UpdateTargetPath();
            }
            if (_treeRebuildQueued)
            {
                foreach (var p in _projects.Values)
                {
                    p.Descriptor.References = Collect(p).Distinct().ToList();
                }
                _cached = _projects.Values.Select(d => d.Descriptor).ToList();
                
            }
            if (targetPathsChanged || _treeRebuildQueued)
                Changed?.Invoke(this, new EventArgs());
            _treeRebuildQueued = false;
        }


        public static void AddChangedHandler(EventHandler<EventArgs> handler)
        {
            WeakEventManager<ProjectInfoService, EventArgs>.AddHandler(Instance, nameof(Changed), handler);
        }

        public event EventHandler<EventArgs> Changed;


        static IEnumerable<Project> GetProjects(IEnumerable<Project> en)
        {
            foreach (var p in en)
            {
                if(p?.Object is VSProject)
                    yield return p;

                if (p?.Object is SolutionFolder)
                    foreach (var item in GetProjects(p.ProjectItems.OfType<ProjectItem>().Select(i => i.SubProject)))
                        yield return item;
            }

        }
    }
}
