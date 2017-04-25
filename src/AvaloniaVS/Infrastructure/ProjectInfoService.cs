using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AvaloniaVS.ViewModels;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using VSLangProj;

namespace AvaloniaVS.Infrastructure
{
    public sealed class ProjectInfoService
    {
        static readonly ProjectInfoService Instance = new ProjectInfoService();
        private DTE2 _dte;
        private List<ProjectDescriptor> _projects = new List<ProjectDescriptor>();

        public static IEnumerable<ProjectDescriptor> Projects => Instance._projects;

        private ProjectInfoService()
        {
            _dte = (DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SDTE));
            new DispatcherTimer() {Interval = new TimeSpan(0, 0, 0, 1), IsEnabled = true}.Tick += OnTick;
            OnTick(null, null);
        }

        public static void AddChangedHandler(EventHandler<EventArgs> handler)
        {
            WeakEventManager<ProjectInfoService, EventArgs>.AddHandler(Instance, nameof(Changed), handler);
        }

        public event EventHandler<EventArgs> Changed;


        IEnumerable<Project> GetProjects(IEnumerable<Project> en)
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

        private void OnTick(object sender, EventArgs e)
        {
            var lst = new List<ProjectDescriptor>();
            try
            {



                foreach (var proj in GetProjects(_dte.Solution.Projects.OfType<Project>()))
                {
                    try
                    {
                        lst.Add(new ProjectDescriptor(proj));
                    }
                    catch
                    {
                        
                    }
                }
            }
            catch
            {
                
            }

            bool changed = _projects.Count != lst.Count ||
                           _projects.Where(
                                   (t, c) => lst[c].Project != t.Project
                                             || lst[c].TargetAssembly != t.TargetAssembly
                                             || !lst[c].References.SequenceEqual(t.References))
                               .Any();
            if (changed)
            {
                _projects = lst;
                Changed?.Invoke(this, new EventArgs());
            }

        }
    }
}
