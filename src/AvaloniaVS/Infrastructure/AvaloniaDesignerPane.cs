using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Avalonia.Designer;
using Avalonia.Ide.CompletionEngine;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using Avalonia.Ide.CompletionEngine.SrmMetadataProvider;
using AvaloniaVS.Helpers;
using AvaloniaVS.ViewModels;
using AvaloniaVS.Views;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace AvaloniaVS.Infrastructure
{
    [ComVisible(true), Guid("75b0ba12-1e01-4f80-a035-32239896bcab")]
    public partial class AvaloniaDesignerPane : WindowPane
    {
        private const int WM_KEYFIRST = 0x0100;
        private const int WM_KEYLAST = 0x0109;

        private AvaloniaDesignerHostView _designerHostView;
        private AvaloniaDesignerHostViewModel _designerHost;
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

        private object GetContent(object adapter)
        {
            return adapter.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.Name == "Content").GetMethod.Invoke(adapter, null);
        }

        private void InitializePane()
        {
            // initialize the designer host view.
            _designerHost = new AvaloniaDesignerHostViewModel(_fileName)
            {
                EditView = GetContent(_vsCodeWindow),
                Orientation = _designerSettings.SplitOrientation == SplitOrientation.Default ||
                              _designerSettings.SplitOrientation == SplitOrientation.Horizontal
                    ? Orientation.Horizontal
                    : Orientation.Vertical,
                IsReversed = _designerSettings.IsReversed
            };
            _designerHostView = new AvaloniaDesignerHostView { DataContext = _designerHost };
            _designerHostView.Init(_designerSettings);

            InitializeDesigner();
            _designerHost.TargetExeChanged += UpdateTargetExe;
        }

        private void UpdateTargetExe(string exe)
        {
            _targetExe = exe;
            _designer.TargetExe = exe;
            _designerHost.DesignView = _targetExe == null
                ? (object)new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Text = $"{Path.GetFileName(_fileName)} cannot be edited in the Design view. Make sure that it's referenced from at least one desktop exe project and rebuild solution. FixMe button below might also help."
                }
                : _designer;
            ReloadMetadata();
        }

        private void InitializeDesigner()
        {
            var basePath =
                Path.GetDirectoryName(typeof(AvaloniaDesignerPane).Assembly.GetModules()[0].FullyQualifiedName);
            basePath = Path.Combine(basePath, "lib");
            _designer = new AvaloniaDesigner(new DesignerConfiguration
            {
                NetFxAppHostPath = Path.Combine(basePath, "Avalonia.Designer.HostApp.exe"),
                NetCoreAppHostPath = Path.Combine(basePath, "Avalonia.Designer.HostApp.dll")
            })
            {
                SourceAssembly = Utils.GetContainerProject(_fileName)?.GetAssemblyPath()
            };
            _designer.SpawnedProcess += DesignerKiller.Register;
            _designerHost.SourceAssemblyChanged += sa => {
                if (_designer != null) {
                    _designer.SourceAssembly = sa;
                }
            };
            UpdateTargetExe(_designerHost.TargetExe);
            _designerHost.RestartDesigner = new RelayCommand(() => {
                Restart();
                ReloadMetadata();
            });
            _designer.Xaml = _textBuffer.CurrentSnapshot.GetText();
            AvaloniaBuildEvents.Instance.BuildEnd += Restart;
            AvaloniaBuildEvents.Instance.ModeChanged += OnModeChanged;
            _textBuffer.PostChanged += OnTextBufferPostChanged;
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

        private void ReloadMetadata()
        {
            if (_targetExe == null || !File.Exists(_targetExe)) {
                return;
            }

            try {
                _textBuffer.Properties[typeof(Metadata)] = new MetadataReader(new SrmMetadataProvider())
                    .GetForTargetAssembly(_targetExe);
            } catch (Exception e) {
                //TODO: Log
            }
        }

        public override object Content => _designerHostView;

        private void EventsOnBuildBegin()
        {
            _designer?.KillProcess();
        }

        private void OnModeChanged()
        {
            var dte = (DTE)Package.GetGlobalService(typeof(DTE));
            if (dte.Mode == vsIDEMode.vsIDEModeDesign) {
                Restart();
            }
        }

        private async void Restart()
        {
            var token = ++_lastRestartToken;
            Console.WriteLine("Designer restart requested, waiting");
            await System.Threading.Tasks.Task.Delay(1000);
            if (token != _lastRestartToken) {
                return;
            }

            var dte = (DTE)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE));
            if (dte.Mode != vsIDEMode.vsIDEModeDesign || AvaloniaBuildEvents.IsBuilding) {
                return;
            }

            try {
                Console.WriteLine("Restarting designer");
                _designer?.RestartProcess();
            } catch {
                //TODO: Log
            }
            ReloadMetadata();
        }

        protected override void Dispose(bool disposing)
        {
            PaneDispose(disposing);
            base.Dispose(disposing);
        }

        private void PaneDispose(bool disposing)
        {
            if (disposing) {
                AvaloniaBuildEvents.Instance.BuildEnd -= Restart;
                AvaloniaBuildEvents.Instance.ModeChanged -= OnModeChanged;
                _designer?.KillProcess();
                _textBuffer.PostChanged -= OnTextBufferPostChanged;
            }
        }
    }
}
