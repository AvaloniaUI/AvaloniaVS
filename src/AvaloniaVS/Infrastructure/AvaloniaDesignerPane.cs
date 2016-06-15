using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Avalonia.Designer;
using AvaloniaVS.Controls;
using AvaloniaVS.Helpers;
using AvaloniaVS.IntelliSense;
using AvaloniaVS.Internals;
using AvaloniaVS.Views;
using Debugger = System.Diagnostics.Debugger;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace AvaloniaVS.Infrastructure
{
    [ComVisible(true), Guid("75b0ba12-1e01-4f80-a035-32239896bcab")]
    public partial class AvaloniaDesignerPane : WindowPane
    {
        private const int WM_KEYFIRST = 0x0100;
        private const int WM_KEYLAST = 0x0109;

        private AvaloniaDesignerHostView _designerHost;
        private readonly string _fileName;
        private readonly IAvaloniaDesignerSettings _designerSettings;
        private AvaloniaDesigner _designer;
        private string _targetExe;
        private long _lastRestartToken;
        private IVsCodeWindow _vsCodeWindow;
        private readonly ITextBuffer _textBuffer;

        public AvaloniaDesignerPane(IVsCodeWindow vsCodeWindow, IVsTextLines textBuffer, string fileName, IAvaloniaDesignerSettings designerSettings)
        {
            _vsCodeWindow = vsCodeWindow;
            _textBuffer = textBuffer.GetTextBuffer();
            _fileName = fileName;
            _designerSettings = designerSettings;
        }

        protected override void Initialize()
        {
            base.Initialize();
            InitializePane();
            RegisterMenuCommands();
        }

        private void InitializePane()
        {
            // initialize the designer host view.
            _designerHost = new AvaloniaDesignerHostView
            {
                EditView =
                {
                    Content = ((WindowPane)_vsCodeWindow).Content
                },
                Container =
                {
                    Orientation = _designerSettings.SplitOrientation == SplitOrientation.Default ||
                                  _designerSettings.SplitOrientation == SplitOrientation.Horizontal
                        ? Orientation.Horizontal
                        : Orientation.Vertical,
                    IsReversed = _designerSettings.IsReversed
                }
            };

            if (_designerSettings.DocumentView != DocumentView.SplitView)
            {
                _designerHost.Container.Collapse(_designerSettings.DocumentView == DocumentView.DesignView ? SplitterViews.Design : SplitterViews.Editor);
            }

            InitializeDesigner();
        }

        private static Project GetContainerProject(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                return null;
            }

            var dte2 = (DTE2) Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof (SDTE));
            var projItem = dte2?.Solution.FindProjectItem(fileName);
            return projItem?.ContainingProject;
        }

        private void InitializeDesigner()
        {
            _targetExe = GetContainerProject(_fileName).GetAssemblyPath();
            if (_targetExe == null)
            {
                var block = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Text = $"{Path.GetFileName(_fileName)} cannot be edited in the Design view."
                };
                _designerHost.DesignView.Content = block;
                return;
            }

            _designer = new AvaloniaDesigner { TargetExe = _targetExe };
            _designerHost.DesignView.Content = _designer;

            _designer.Xaml = _textBuffer.CurrentSnapshot.GetText();
            AvaloniaBuildEvents.Instance.BuildEnd += Restart;
            AvaloniaBuildEvents.Instance.ModeChanged += OnModeChanged;
            _textBuffer.PostChanged += OnTextBufferPostChanged;
            ReloadMetadata();
        }

        private void OnTextBufferPostChanged(object sender, EventArgs e)
        {
            var buffer = (ITextBuffer)sender;
            _designer.Xaml = buffer.CurrentSnapshot.GetText();
        }

        protected override void OnClose()
        {
            _vsCodeWindow.Close();
            base.OnClose();
        }

        void ReloadMetadata()
        {
            if (!File.Exists(_targetExe))
            {
                return;
            }

            _textBuffer.Properties[typeof (Metadata)] = MetadataLoader.LoadMetadata(_targetExe);
        }

        public override object Content
        {
            get
            {
                return _designerHost;
            }
        }

        private void EventsOnBuildBegin()
        {
            _designer?.KillProcess();
        }

        private void OnModeChanged()
        {
            var dte = (DTE)Package.GetGlobalService(typeof(DTE));
            if (dte.Mode == vsIDEMode.vsIDEModeDesign)
                Restart();
        }

        private async void Restart()
        {
            long token = ++_lastRestartToken;
            Console.WriteLine("Designer restart requested, waiting");
            await System.Threading.Tasks.Task.Delay(1000);
            if (token != _lastRestartToken)
                return;
            var dte = (DTE)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE));
            if (dte.Mode != vsIDEMode.vsIDEModeDesign)
                return;
            try
            {
                Console.WriteLine("Restarting designer");
                _designer?.RestartProcess();
            }
            catch
            {
                //TODO: Log
            }
            try
            {
                ReloadMetadata();
            }
            catch
            {
                //TODO: Log
            }
        }

        protected override void Dispose(bool disposing)
        {
            PaneDispose(disposing);
            base.Dispose(disposing);
        }

        private void PaneDispose(bool disposing)
        {
            if (disposing)
            {
                AvaloniaBuildEvents.Instance.BuildEnd -= Restart;
                AvaloniaBuildEvents.Instance.ModeChanged -= OnModeChanged;
                _designer?.KillProcess();
                _textBuffer.PostChanged -= OnTextBufferPostChanged;
            }
        }
    }
}