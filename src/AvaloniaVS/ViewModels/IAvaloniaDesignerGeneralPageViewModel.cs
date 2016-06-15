using AvaloniaVS.Infrastructure;

namespace AvaloniaVS.ViewModels
{
    public interface IAvaloniaDesignerGeneralPageViewModel
    {
        bool IsDesignerEnabled { get; set; }
        bool IsReversed { get; set; }
        DocumentView DocumentView { get; set; }
        SplitOrientation SplitOrientation { get; set; }
    }
}