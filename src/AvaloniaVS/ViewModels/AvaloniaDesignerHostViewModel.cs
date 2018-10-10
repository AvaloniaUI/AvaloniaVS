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
using System.Windows.Input;
using AvaloniaVS.Infrastructure;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using PropertyChanged;

namespace AvaloniaVS.ViewModels
{
    public class PreviewerRunTarget
    {
        public string Name { get; set; }

        public string TargetAssembly { get; set; }

        public override string ToString() => Name;
    }

    public class AvaloniaDesignerHostViewModel : PropertyChangedBase, IAvaloniaDesignerHostViewModel
    {
        private readonly string _fileName;
        private PreviewerRunTarget _selectedTarget;
        private string _sourceAssembly;

        public object EditView { get; set; }

        public object DesignView { get; set; }

        public bool ShowTargetSelector { get; set; }

        public Orientation Orientation { get; set; }

        public bool IsReversed { get; set; }

        public List<PreviewerRunTarget> AvailableTargets { get; set; }

        public ICommand RestartDesigner { get; set; }

        public event Action<string> TargetExeChanged;

        public event Action<string> SourceAssemblyChanged;

        public PreviewerRunTarget SelectedTarget
        {
            get
            {
                return _selectedTarget;
            }

            set
            {
                _selectedTarget = value;
                OnPropertyChanged();
                TargetExe = _selectedTarget?.TargetAssembly;
                TargetExeChanged?.Invoke(TargetExe);
            }
        }

        public string TargetExe { get; set; }

        public string SourceAssembly
        {
            get
            {
                return _sourceAssembly;
            }

            set
            {
                _sourceAssembly = value;
                OnPropertyChanged();
                SourceAssemblyChanged?.Invoke(value);
            }
        }

        public AvaloniaDesignerHostViewModel(string fileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _fileName = fileName;
            PopulateTargetList();
            ProjectInfoService.AddChangedHandler(OnProjectInfoChanged);
        }

        private void OnProjectInfoChanged(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            PopulateTargetList();
            SourceAssembly = Utils.GetContainerProject(_fileName).GetAssemblyPath();
        }

        private void PopulateTargetList()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var containing = Utils.GetContainerProject(_fileName);
            AvailableTargets = new List<PreviewerRunTarget>(
                ProjectInfoService.Projects.Where(p => p.Project == containing || p.References.Contains(containing))
                    .OrderBy(p => p.Project == containing)
                    .SelectMany(p => p.RunnableOutputs.Select(o => new PreviewerRunTarget
                    {
                        Name = $"{p.Name} [{o.Key}]",
                        TargetAssembly = o.Value
                    })));

            SelectedTarget = (SelectedTarget == null
                ? null
                : AvailableTargets.FirstOrDefault(a => a.TargetAssembly == SelectedTarget.TargetAssembly)) ??
                             AvailableTargets.FirstOrDefault();
            ShowTargetSelector = AvailableTargets.Count > 1;
        }
    }
}
