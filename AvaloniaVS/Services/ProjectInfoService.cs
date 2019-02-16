using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using AvaloniaVS.Models;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VSLangProj;

namespace AvaloniaVS.Services
{
    internal sealed class ProjectInfoService
    {
        private static readonly ProjectInfoService Instance = new ProjectInfoService();

        private readonly DTE2 _dte;
        private readonly ProjectItemsEvents _solutionItemEvents;
        private readonly SolutionEvents _solutionEvents;
        private bool _solutionRescanQueued = true;
        private bool _targetPathRescanQueued = true;
        private bool _treeRebuildQueued = true;
        private BuildEvents _buildEvents;

        public static IEnumerable<ProjectDescriptor> Projects => Instance._cached;

        private class ProjectEntry
        {
            private readonly ProjectInfoService _service;
            private ReferencesEvents _events;
            public Project Project { get; }
            public HashSet<Project> ProjectReferences = new HashSet<Project>();
            public HashSet<string> References = new HashSet<string>();
            public ProjectDescriptor Descriptor { get; }
            public bool RescanQueued { get; private set; }
            public bool TargetPathUpdateQueued { get; private set; }

            public void CheckForLoadedStateChanges()
            {
                var project = Project.Cast<VSProject>();
                if (project != null && _events == null)
                {
                    SetupEvents(project);
                    Rescan();
                }
                if (project == null && _events != null)
                {
                    _events = null;
                    ProjectReferences.Clear();
                    References.Clear();
                    _service._treeRebuildQueued = true;
                    TargetPathUpdateQueued = true;
                }
            }

            private void SetupEvents(VSProject vsProject)
            {
                _events = vsProject.Events.ReferencesEvents;
                _events.ReferenceAdded += r =>
                {
                    var p = r.SourceProject;
                    if (p != null)
                        ProjectReferences.Add(p);
                    else
                        References.Add(r.Name);
                    _service._treeRebuildQueued = true;
                };
                _events.ReferenceRemoved += r =>
                {
                    var p = r.SourceProject;
                    if (p != null)
                        ProjectReferences.Remove(p);
                    else
                        References.Remove(r.Name);
                    _service._treeRebuildQueued = true;
                };
                _events.ReferenceChanged += delegate
                {
                    RescanQueued = true;
                };
            }

            public ProjectEntry(ProjectInfoService service,  Project project)
            {
                _service = service;
                Project = project;
                Descriptor = new ProjectDescriptor(project);
                var vsProject = project.Cast<VSProject>();
                if (vsProject != null)
                    SetupEvents(vsProject);
                RescanQueued = TargetPathUpdateQueued = true;
            }

            public void Rescan()
            {
                ProjectReferences.Clear();
                References.Clear();
                var vsProject = Project.Cast<VSProject>();
                if (vsProject != null)
                    foreach (Reference r in vsProject.References)
                    {
                        var p = r.SourceProject;
                        if (p != null)
                            ProjectReferences.Add(p);
                        else
                            References.Add(r.Name);
                    }
                RescanQueued = false;
                _service._treeRebuildQueued = true;
            }

            public bool UpdateTargetPath()
            {
                var n = Project.GetAssemblyPath();
                TargetPathUpdateQueued = false;
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
            ThreadHelper.ThrowIfNotOnUIThread();

            _dte = (DTE2)Package.GetGlobalService(typeof(SDTE));
            _solutionItemEvents = _dte.Events.SolutionItemsEvents;
            _solutionItemEvents.ItemAdded += delegate { _solutionRescanQueued = true; };
            _solutionItemEvents.ItemRenamed += delegate { _solutionRescanQueued = true; };
            _solutionItemEvents.ItemRemoved += delegate { _solutionRescanQueued = true; };
            _solutionEvents = _dte.Events.SolutionEvents;
            _solutionEvents.Opened += delegate { _solutionRescanQueued = true; };

            new DispatcherTimer() { Interval = new TimeSpan(0, 0, 0, 1), IsEnabled = true }.Tick += OnTick;
            OnTick(null, null);

            _buildEvents = _dte.Events.BuildEvents;
            _buildEvents.OnBuildDone += delegate
            {
                _targetPathRescanQueued = true;
            };
        }

        private IEnumerable<Project> Collect(ProjectEntry desc)
        {
            foreach (var r in desc.ProjectReferences)
            {
                yield return r;
                if(_projects.TryGetValue(r, out var found))
                    foreach (var ch in Collect(found))
                        yield return ch;
            }
        }
                
        private void OnTick(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dic = new Dictionary<Project, ProjectEntry>();

            foreach (var p in GetProjects(_dte.Solution.Projects.OfType<Project>()))
            {
                if (_projects.TryGetValue(p, out var existing))
                    dic[p] = existing;
                else
                {
                    dic[p] = new ProjectEntry(this, p);
                    _treeRebuildQueued = true;
                }
            }
            if (dic.Count != _projects.Count)
                _treeRebuildQueued = true;
            _projects = dic;
                
            
            bool targetPathsChanged = false;
            foreach (var p in _projects.Values)
            {
                p.CheckForLoadedStateChanges();
                if (p.RescanQueued)
                    p.Rescan();
                if (_targetPathRescanQueued || p.TargetPathUpdateQueued)
                    targetPathsChanged |= p.UpdateTargetPath();
                
            }
            _targetPathRescanQueued = false;
            if (_treeRebuildQueued)
            {
                foreach (var p in _projects.Values)
                {
                    p.Descriptor.ProjectReferences = Collect(p).Distinct().ToList();
                    p.Descriptor.References = p.References.ToList();
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

        private static IEnumerable<Project> GetProjects(IEnumerable<Project> en)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (var p in en)
            {
                if (p.Cast<VSProject>() != null)
                    yield return p;

                if (p.Cast<SolutionFolder>() != null)
                    foreach (var item in GetProjects(p.ProjectItems.OfType<ProjectItem>().Select(i => i.SubProject)))
                        yield return item;
            }

        }
    }
}
