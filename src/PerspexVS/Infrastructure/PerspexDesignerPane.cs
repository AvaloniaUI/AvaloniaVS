using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Perspex.Designer;
using PerspexVS.IntelliSense;
using PerspexVS.Views;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace PerspexVS.Infrastructure
{
    [ComVisible(true), Guid("75b0ba12-1e01-4f80-a035-32239896bcab")]
    public partial class PerspexDesignerPane : ToolWindowPane
    {
        private const int WM_KEYFIRST = 0x0100;
        private const int WM_KEYLAST = 0x0109;

        private PerspexDesignerHostView _designerHost;
        private readonly IWpfTextViewHost _wpfTextViewHost;
        private readonly IVsTextLines _textBuffer;
        private readonly IVsTextView _textView;
        private readonly string _fileName;
        private IVsFindTarget _vsFindTarget;
        private PerspexDesigner _designer;
        private string _targetExe;
        private long _lastRestartToken;

        public PerspexDesignerPane(IWpfTextViewHost wpfTextViewHost, IVsTextLines textBuffer, IVsTextView textView, string fileName)
        {
            _wpfTextViewHost = wpfTextViewHost;
            _textBuffer = textBuffer;
            _textView = textView;
            _fileName = fileName;
        }

        protected override void Initialize()
        {
            base.Initialize();
            InitializeDesigner();
            RegisterMenuCommands();
        }

        private void InitializeDesigner()
        {
            // initialize the designer host view.
            _designerHost = new PerspexDesignerHostView
            {
                EditView =
                {
                    Content = _wpfTextViewHost
                }
            };

            // initialize the designer
            var wpfTextView = _wpfTextViewHost.TextView;

            _targetExe = wpfTextView.GetContainingProject()?.GetAssemblyPath();
            if (_targetExe == null)
            {
                var block = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Text = $"{Path.GetFileName(_fileName)} cannot be edited in the Design view."
                };
                _designerHost.EditView.Content = block;
                return;
            }

            _designer = new PerspexDesigner { TargetExe = _targetExe };
            _designerHost.DesignView.Content = _designer;

            _designer.Xaml = wpfTextView.TextBuffer.CurrentSnapshot.GetText();
            PerspexBuildEvents.Instance.BuildEnd += Restart;
            PerspexBuildEvents.Instance.ModeChanged += OnModeChanged;
            wpfTextView.TextBuffer.PostChanged += delegate
            {
                _designer.Xaml = wpfTextView.TextBuffer.CurrentSnapshot.GetText();
            };
            ReloadMetadata();
        }

        void ReloadMetadata()
        {
            if (File.Exists(_targetExe))
                _wpfTextViewHost.TextView.TextBuffer.Properties[typeof(Metadata)] = MetadataLoader.LoadMetadata(_targetExe);
        }

        public override object Content
        {
            get
            {
                return _designerHost;
            }
        }

        private IVsFindTarget VsFindTarget
        {
            get { return _vsFindTarget ?? (_vsFindTarget = (IVsFindTarget) _textView); }
        }

        public override void OnToolWindowCreated()
        {
            var frame = Frame as IVsWindowFrame;
            if (frame != null)
            {
                var textEditorFactoryGuid = VSConstants.GUID_TextEditorFactory;
                frame.SetGuidProperty((int)__VSFPROPID.VSFPROPID_InheritKeyBindings, ref textEditorFactoryGuid);
            }

            base.OnToolWindowCreated();
        }

        private void EventsOnBuildBegin()
        {
            _designer?.KillProcess();
        }

        private void OnModeChanged()
        {
            var dte = (DTE)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE));
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
                PerspexBuildEvents.Instance.BuildBegin -= EventsOnBuildBegin;
                PerspexBuildEvents.Instance.BuildEnd -= Restart;
                _designer?.KillProcess();
            }
        }
    }
}