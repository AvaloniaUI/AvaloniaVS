using PerspexVS.Infrastructure;
using PerspexVS.Options;
using PerspexVS.ViewModels;

namespace PerspexVS.Helpers
{
    internal class PerspexDesignerGeneralPageViewModelDesign : IPerspexDesignerGeneralPageViewModel
    {
        public bool IsDesignerEnabled { get; set; }
        public bool IsReversed { get; set; }
        public DocumentView DocumentView { get; set; }
        public SplitOrientation SplitOrientation { get; set; }
    }
}