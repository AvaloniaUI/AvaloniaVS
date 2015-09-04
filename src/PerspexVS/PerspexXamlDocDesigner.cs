using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Perspex.Designer;

namespace PerspexVS
{
    /// <summary>
    /// Include the necessary logic to inject the Perspex Designer above the default Xaml Designer
    /// </summary>
    internal sealed class PerspexXamlDocDesigner
    {
        private readonly PerspexDesigner Designer;
        private readonly IWpfTextView view;
        private readonly ElementHost DesignView;

        public PerspexXamlDocDesigner(IWpfTextView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }
            this.view = view;
            this.view.TextBuffer.Changed += this.TextBufferOnChanged;
            DesignView = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.GreenYellow,
            };
            SetupDesignerView(view);
            DesignView.Child = Designer = new PerspexDesigner
            {
                TargetExe = @"C:\Users\Darnell\Documents\GitHub\Perspex\samples\XamlTestApplication\bin\Debug\XamlTestApplication.exe",
                Xaml = this.view.TextBuffer.CurrentSnapshot.GetText()
            };
        }

        private void SetupDesignerView(IWpfTextView view)
        {
            var WpfControl = (System.Windows.Controls.Control)view;
            var timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(50), IsEnabled = true };
            timer.Tick += delegate
            {
                var wpfWindow = ((System.Windows.Interop.HwndSource)PresentationSource.FromVisual(WpfControl)).Handle;
                var control = System.Windows.Forms.Control.FromChildHandle(wpfWindow);
                var otherPanel = control?.Parent.Controls.OfType<System.Windows.Forms.Panel>()
                    .FirstOrDefault(p => p != control);

                if (otherPanel == null) return;
                otherPanel.Controls.Add(DesignView);
                timer.IsEnabled = false;
            };

            DesignView.Visible =
                this.view.TextBuffer.CurrentSnapshot.GetText()
                    .ToLower()
                    .Contains("=\"https://github.com/grokys/perspex\"");
        }

        private void TextBufferOnChanged(object sender, TextContentChangedEventArgs e)
        {
            var text = e.After.GetText();
            DesignView.Visible = text.ToLower().Contains("=\"https://github.com/grokys/perspex\"");
            Designer.Xaml = text;
        }
    }
}
