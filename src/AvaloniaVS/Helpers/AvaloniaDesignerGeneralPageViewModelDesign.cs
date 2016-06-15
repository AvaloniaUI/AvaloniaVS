using AvaloniaVS.Infrastructure;
using AvaloniaVS.Options;
using AvaloniaVS.ViewModels;

namespace AvaloniaVS.Helpers
{
    internal class AvaloniaDesignerGeneralPageViewModelDesign : IAvaloniaDesignerGeneralPageViewModel
    {
        public bool IsDesignerEnabled { get; set; }
        public bool IsReversed { get; set; }
        public DocumentView DocumentView { get; set; }
        public SplitOrientation SplitOrientation { get; set; }
    }
}