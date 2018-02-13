using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace AvaloniaVS.ViewModels
{
    public interface IAvaloniaDesignerHostViewModel
    {
        List<PreviewerRunTarget> AvailableTargets { get; set; }
        object DesignView { get; set; }
        object EditView { get; set; }
        bool IsReversed { get; set; }
        bool ShowTargetSelector { get;  }
        Orientation Orientation { get; set; }
        PreviewerRunTarget SelectedTarget { get; set; }
        ICommand RestartDesigner { get; set; }
    }

    public class AvaloniaDesignerHostViewModelMock : IAvaloniaDesignerHostViewModel
    {
        public List<PreviewerRunTarget> AvailableTargets { get; set; }
        public object DesignView { get; set; }
        public object EditView { get; set; }
        public bool IsReversed { get; set; }
        public bool ShowTargetSelector => true;
        public Orientation Orientation { get; set; }
        public ICommand RestartDesigner { get; set; }

        public PreviewerRunTarget SelectedTarget { get; set; } =
            new PreviewerRunTarget {Name = "DUMMY", TargetAssembly = "DUMMY"};

        public AvaloniaDesignerHostViewModelMock()
        {
            AvailableTargets = new List<PreviewerRunTarget> {SelectedTarget};
        }
    }
}