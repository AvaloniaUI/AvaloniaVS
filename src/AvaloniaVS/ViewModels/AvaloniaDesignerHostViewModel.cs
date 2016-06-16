using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AvaloniaVS.Infrastructure;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using PropertyChanged;

namespace AvaloniaVS.ViewModels
{


    public class AvaloniaDesignerHostViewModel : PropertyChangedBase, IAvaloniaDesignerHostViewModel
    {
        private readonly string _fileName;
        private ProjectDescriptor _selectedTarget;

        public object EditView { get; set; }
        public object DesignView { get; set; }
        public bool ShowTargetSelector { get; set; }
        public Orientation Orientation { get; set; }
        public bool IsReversed { get; set; }
        public List<ProjectDescriptor> AvailableTargets { get; set; }
        public event Action<string> TargetExeChanged;

        public ProjectDescriptor SelectedTarget
        {
            get { return _selectedTarget; }
            set
            {
                _selectedTarget = value;
                OnPropertyChanged();
                TargetExe = _selectedTarget?.TargetAssembly;
                TargetExeChanged?.Invoke(TargetExe);
            }
        }

        public string TargetExe { get; set; }

        public AvaloniaDesignerHostViewModel(string fileName)
        {
            _fileName = fileName;
            PopulateTargetList();
            ProjectInfoService.AddChangedHandler(OnProjectInfoChanged);
        }

        private void OnProjectInfoChanged(object sender, EventArgs e)
        {
            PopulateTargetList();
        }


        void PopulateTargetList()
        {
            var containing = Utils.GetContainerProject(_fileName);
            AvailableTargets = new List<ProjectDescriptor>(
                ProjectInfoService.Projects.Where(p => p.TargetAssembly?.ToLower()?.EndsWith(".exe") == true
                                                       && (p.Project == containing || p.References.Contains(containing)))
                    .OrderBy(p => p.Project == containing));

            SelectedTarget = (SelectedTarget == null
                ? null
                : AvailableTargets.FirstOrDefault(a => a.Project == SelectedTarget.Project)) ??
                             AvailableTargets.FirstOrDefault();
            ShowTargetSelector = AvailableTargets.Count > 1;
        }

        
    }
}
