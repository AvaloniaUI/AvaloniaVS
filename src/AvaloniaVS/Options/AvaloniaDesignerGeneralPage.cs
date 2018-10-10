using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using AvaloniaVS.Internals;
using AvaloniaVS.ViewModels;
using AvaloniaVS.Views;
using Microsoft.VisualStudio.Shell;

namespace AvaloniaVS.Options
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [Guid(Guids.AvaloniaDesignerGeneralPageString)]
    public class AvaloniaDesignerGeneralPage : UIElementDialogPage
    {
        private AvaloniaDesignerGeneralPageView _view;

        protected override UIElement Child
        {
            get
            {
                LoadView();
                return _view ?? default;
            }
        }

        private void LoadView()
        {
            try {
                _view = VisualStudioServices.ComponentModel.DefaultExportProvider.GetExportedValue<AvaloniaDesignerGeneralPageView>();
            } catch {
            }
        }

        /// <summary>
        /// Handles Apply messages from the Visual Studio environment.
        /// </summary>
        /// <param name="e">[in] Arguments to event handler.</param>
        protected override void OnApply(PageApplyEventArgs e)
        {
            Debug.Assert(_view != null);
            var viewModel = _view?.DataContext as AvaloniaDesignerGeneralPageViewModel;
            Debug.Assert(viewModel != null);
            viewModel?.ApplyChanges();
            base.OnApply(e);
        }
    }
}
