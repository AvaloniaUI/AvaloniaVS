using PerspexVS.Infrastructure;

namespace PerspexVS.ViewModels
{
    public interface IPerspexDesignerGeneralPageViewModel
    {
        bool IsDesignerEnabled { get; set; }
        bool IsReversed { get; set; }
        DocumentView DocumentView { get; set; }
        SplitOrientation SplitOrientation { get; set; }
    }
}