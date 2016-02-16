using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using PerspexVS.Internals;
using PerspexVS.ViewModels;
using PerspexVS.Views;

namespace PerspexVS.Options
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [Guid(Guids.PerspexDesignerGeneralPageString)]
    public class PerspexDesignerGeneralPage : UIElementDialogPage
    {
        private PerspexDesignerGeneralPageView _view;

        protected override UIElement Child
        {
            get
            {
                LoadView();
                return _view;
            }
        }

        private void LoadView()
        {
            _view = VisualStudioServices.ComponentModel.DefaultExportProvider.GetExportedValue<PerspexDesignerGeneralPageView>();
        }

        /// <summary>
        /// Handles Apply messages from the Visual Studio environment.
        /// </summary>
        /// <param name="e">[in] Arguments to event handler.</param>
        protected override void OnApply(PageApplyEventArgs e)
        {
            Debug.Assert(_view != null);
            var viewModel = _view.DataContext as PerspexDesignerGeneralPageViewModel;
            Debug.Assert(viewModel != null);
            viewModel.ApplyChanges();
            base.OnApply(e);
        }
    }
}