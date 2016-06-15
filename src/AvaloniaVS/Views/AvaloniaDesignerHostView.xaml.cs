using AvaloniaVS.Controls;
using AvaloniaVS.Infrastructure;

namespace AvaloniaVS.Views
{
    public partial class AvaloniaDesignerHostView
    {
        public AvaloniaDesignerHostView()
        {
            InitializeComponent();
        }

        public void Init(IAvaloniaDesignerSettings designerSettings)
        {
            if (designerSettings.DocumentView != DocumentView.SplitView)
            {
                Container.Collapse(designerSettings.DocumentView == DocumentView.DesignView
                    ? SplitterViews.Design
                    : SplitterViews.Editor);
            }
        }
    }
}
