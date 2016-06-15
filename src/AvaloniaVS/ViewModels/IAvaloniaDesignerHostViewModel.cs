using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace AvaloniaVS.ViewModels
{
    public interface IAvaloniaDesignerHostViewModel
    {
        List<ProjectDescriptor> AvailableTargets { get; set; }
        object DesignView { get; set; }
        object EditView { get; set; }
        bool IsReversed { get; set; }
        bool ShowTargetSelector { get;  }
        Orientation Orientation { get; set; }
        ProjectDescriptor SelectedTarget { get; set; }
    }

    public class AvaloniaDesignerHostViewModelMock : IAvaloniaDesignerHostViewModel
    {
        public List<ProjectDescriptor> AvailableTargets { get; set; }
        public object DesignView { get; set; }
        public object EditView { get; set; }
        public bool IsReversed { get; set; }
        public bool ShowTargetSelector => true;
        public Orientation Orientation { get; set; }
        public ProjectDescriptor SelectedTarget { get; set; } = new ProjectDescriptor("DUMMY");

        public AvaloniaDesignerHostViewModelMock()
        {
            AvailableTargets = new List<ProjectDescriptor> {SelectedTarget};
        }
    }
}