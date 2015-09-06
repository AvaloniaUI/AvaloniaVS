using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using System.Xml.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Perspex.Designer;

namespace PerspexVS
{
    /// <summary>
    /// Includes the necessary logic to inject the <see cref="PerspexDesigner"/> above the default Xaml Designer as well
    /// as updating the designer's Xaml
    /// </summary>
    internal sealed class PerspexXamlDocDesigner
    {
        private IWpfTextView _view;
        private PerspexDesigner _designer;
        private ElementHost _designView;

        /// <summary>
        /// Initializes view and build events as well as performs <see cref="PerspexDesigner"/> injection
        /// </summary>
        /// <param name="view"></param>
        /// <param name="filePath"></param>
        public PerspexXamlDocDesigner(IWpfTextView view, string filePath)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }
            this._view = view;
            this._view.TextBuffer.Changed += this.TextBufferOnChanged;
            this._view.Closed += ViewOnClosed;
            Project p = GetContainingProject(filePath); 
            _designer = new PerspexDesigner
            {
                TargetExe = GetAssemblyPath(p),
                Xaml = this._view.TextBuffer.CurrentSnapshot.GetText()
            };
            SetupDesignerView(_view);
            XamlDocListener.Events.BuildBegin += EventsOnBuildBegin;
            XamlDocListener.Events.BuildEnd += EventsOnBuildEnd;
            p = null;
        }

        private void EventsOnBuildEnd()
        {
            _designer?.RestartProcess();
        }

        private void EventsOnBuildBegin()
        {
            _designer?.KillProcess();
        }

        /// <summary>
        /// Kills the designer and cleans up all class scope references.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void ViewOnClosed(object sender, EventArgs eventArgs)
        {
            XamlDocListener.Events.BuildBegin -= EventsOnBuildBegin;
            XamlDocListener.Events.BuildEnd -= EventsOnBuildEnd;
            _designer.KillProcess();
            XamlDocListener.Instance.PerspexXamlDocDesigners.Remove(this);
            _designer = null;
            _designView = null;
            _view = null;
        }

        /// <summary>
        /// <see cref="PerspexDesigner"/> injection logic moved to a seperate method
        /// to clean up contructor.
        /// </summary>
        /// <param name="view"></param>
        private void SetupDesignerView(IWpfTextView view)
        {
            var WpfControl = (System.Windows.Controls.Control)view;
            var timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(50), IsEnabled = true };
            timer.Tick += delegate
            {
                var hwndSource = PresentationSource.FromVisual(WpfControl) as System.Windows.Interop.HwndSource;
                if (hwndSource == null) return;
                var wpfWindow = hwndSource.Handle;
                var control = System.Windows.Forms.Control.FromChildHandle(wpfWindow);
                var otherPanel = control?.Parent.Controls.OfType<System.Windows.Forms.Panel>()
                    .FirstOrDefault(p => p != control);

                if (otherPanel == null) return;
                _designView = new ElementHost
                {
                    Dock = DockStyle.Fill,
                    BackColor = System.Drawing.Color.Transparent,
                };
                otherPanel.Controls.Add(_designView);
                CheckIfShouldBeVisibile(view.TextBuffer.CurrentSnapshot.GetText());
                _designView.Child = _designer;
                timer.IsEnabled = false;
            };
        }

        /// <summary>
        /// Gives <see cref="PerspexDesigner"/> updated text as the user types.
        /// Also check to ensure the <see cref="PerspexDesigner"/> does not show if the users
        /// is not develping a Perspex application.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBufferOnChanged(object sender, TextContentChangedEventArgs e)
        {
            var text = e.After.GetText();
            _designer.Xaml = text;
            CheckIfShouldBeVisibile(text);
        }

        /// <summary>
        /// Checks for the existance of Perspex namespace and sets <see cref="PerspexDesigner"/>
        /// visibility respectively
        /// </summary>
        /// <param name="text">The Xaml taken from the <see cref="IWpfTextView"/></param>
        private void CheckIfShouldBeVisibile(string text)
        {
            try
            {
                XDocument xdoc = XDocument.Parse(text);
                var result = xdoc.Root.ToString()
                                      .Split(new string[] { Environment.NewLine }, StringSplitOptions.None)
                                      .First();
                _designView.Visible = result.ToLower().Contains("=\"https://github.com/grokys/perspex\"");
            }
            catch (Exception ex)
            {
                // Fallback for some parse errors, may still cause undesired behavior.
                _designView.Visible = text.ToLower().Contains("=\"https://github.com/grokys/perspex\"");
            }
        }

        /// <summary>
        /// Gets the <see cref="Project"/> the specified file
        /// belongs to.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns><see cref="Project"/> or null</returns>
        private Project GetContainingProject(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            var dte2 = (DTE2) Package.GetGlobalService(typeof (SDTE));
            var projItem = dte2?.Solution.FindProjectItem(fileName);
            return projItem?.ContainingProject;
        }

        /// <summary>
        /// Gets the full path of the <see cref="Project"/> configuration
        /// </summary>
        /// <param name="vsProject"></param>
        /// <returns>Target Exe path</returns>
        private string GetAssemblyPath(Project vsProject)
        {
            string fullPath = vsProject.Properties.Item("FullPath").Value.ToString();
            string outputPath = vsProject.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value.ToString();
            string outputDir = Path.Combine(fullPath, outputPath);
            string outputFileName = vsProject.Properties.Item("OutputFileName").Value.ToString();
            string assemblyPath = Path.Combine(outputDir, outputFileName);
            return assemblyPath;
        }
    }
}
