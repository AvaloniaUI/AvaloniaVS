using System;
using System.Diagnostics;
using System.Windows.Navigation;
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

            Container.ActiveViewChanged += (s, e) => IsDesingerVisibleChanged?.Invoke();
        }

        internal Action IsDesingerVisibleChanged;

        internal bool IsDesingerVisible
            => Container?.IsCollapsed == false || Container?.IsDesignerActive == true;

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(e.Uri.ToString());
            }
            catch
            {
                
            }
        }
    }
}
