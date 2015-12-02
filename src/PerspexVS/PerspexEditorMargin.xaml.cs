using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using dnlib.DotNet.Writer;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Perspex.Designer;
using PerspexVS.Infrastructure;
using PerspexVS.IntelliSense;

namespace PerspexVS
{
    public partial class PerspexEditorMargin : UserControl, IWpfTextViewMargin
    {
        private readonly IWpfTextView _textView;
        private readonly PerspexDesigner _designer;
        private string _targetExe;

        public PerspexEditorMargin(IWpfTextView textView)
        {
            _textView = textView;

            _targetExe = textView.GetContainingProject()?.GetAssemblyPath();
            if (_targetExe == null)
            {
                Height = 0;
                return;
            }
            _designer = new PerspexDesigner() {TargetExe = _targetExe };
            InitializeComponent();
            DesignerContainer.Content = _designer;
            Height = 200;
            _designer.Xaml = textView.TextBuffer.CurrentSnapshot.GetText();
            PerspexBuildEvents.Instance.BuildEnd += Restart;
            PerspexBuildEvents.Instance.ModeChanged += OnModeChanged;
            textView.TextBuffer.PostChanged += delegate
            {
                _designer.Xaml = textView.TextBuffer.CurrentSnapshot.GetText();
            };
            ReloadMetadata();
        }

        private void OnModeChanged()
        {
            var dte = (DTE)Package.GetGlobalService(typeof(DTE));
            if(dte.Mode==vsIDEMode.vsIDEModeDesign)
                Restart();
        }

        public PerspexDesigner Designer => _designer;

        private long _lastRestartToken;
        private async void Restart()
        {
            long token = ++_lastRestartToken;
            Console.WriteLine("Designer restart requested, waiting");
            await System.Threading.Tasks.Task.Delay(1000);
            if (token != _lastRestartToken)
                return;
            var dte = (DTE)Package.GetGlobalService(typeof(DTE));
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

        private void EventsOnBuildBegin()
        {
            _designer?.KillProcess();
        }

        void ReloadMetadata()
        {
            if (File.Exists(_targetExe))
                _textView.TextBuffer.Properties[typeof (Metadata)] = MetadataLoader.LoadMetadata(_targetExe);
        }

        public void Dispose()
        {
            PerspexBuildEvents.Instance.BuildBegin -= EventsOnBuildBegin;
            PerspexBuildEvents.Instance.BuildEnd -= Restart;
            _designer.KillProcess();
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return this;
        }

        public double MarginSize => Height;
        public bool Enabled { get; } = true;
        public FrameworkElement VisualElement => this;

        private void OnResizeDrag(object sender, DragDeltaEventArgs e)
        {
            var change =  e.VerticalChange;
            if (Height + change < 100)
                change = 0;
            if (_textView.ViewportHeight - change < 100)
                change = 0;
            Height += change;
        }
    }
}
