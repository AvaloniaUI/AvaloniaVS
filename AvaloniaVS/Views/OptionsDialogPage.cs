using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell;

namespace AvaloniaVS.Views
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    [Guid("3093ca7c-c764-4547-a7ae-12055b139bdf")]
    public class OptionsDialogPage : UIElementDialogPage
    {
        private OptionsView _options;

        protected override UIElement Child => _options ?? (_options = new OptionsView());

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);

            _options.Settings = Site.GetMefService<IAvaloniaVSSettings>();
        }

        public override void SaveSettingsToStorage()
        {
            base.SaveSettingsToStorage();

            _options?.Settings?.Save();
        }

        public override void LoadSettingsFromStorage()
        {
            base.LoadSettingsFromStorage();

            _options?.Settings?.Load();
        }
    }
}
