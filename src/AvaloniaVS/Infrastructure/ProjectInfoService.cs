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

        private void OnTick(object sender, EventArgs e)
        {
            var lst = new List<ProjectDescriptor>();
            try
            {
                lst.AddRange(_dte.Solution.Projects.OfType<Project>().Where(p => p.Object is VSProject).Select(proj => new ProjectDescriptor(proj)));
            }
            catch
            {
                
            }

            bool changed = _projects.Count != lst.Count ||
                           _projects.Where(
                               (t, c) => lst[c].Project != t.Project || !lst[c].References.SequenceEqual(t.References))
                               .Any();
            if (changed)
            {
                _projects = lst;
                Changed?.Invoke(this, new EventArgs());
            }

        }
    }
}
