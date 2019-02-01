using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;
using Avalonia.Remote.Protocol.Designer;
using AvaloniaVS.Models;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Serilog;
using CompletionMetadata = Avalonia.Ide.CompletionEngine.Metadata;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS.Views
{
    public class DesignerPane : EditorHostPane
    {
        private static readonly ILogger s_log = Log.ForContext<DesignerPane>();
        private readonly string _fileName;
        private readonly IWpfTextViewHost _xmlEditor;
        private AvaloniaDesigner _content;

        public DesignerPane(string fileName, IVsCodeWindow xmlEditorWindow, IWpfTextViewHost xmlEditor)
            : base(xmlEditorWindow)
        {
            _fileName = fileName;
            _xmlEditor = xmlEditor;
        }

        public override object Content => _content;
        public PreviewerProcess Process { get; private set; }

        public event EventHandler Initialized;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _content?.Dispose();
            _content = null;
            Process?.Dispose();
            Process = null;
        }

        protected override void Initialize()
        {
            Log.Logger.Verbose("Started DesignerPane.Initialize()");

            base.Initialize();

            var xamlEditorView = new AvaloniaDesigner
            {
                XmlEditor = _xmlEditor,
            };

            _content = xamlEditorView;
            StartEditorAsync(xamlEditorView).FireAndForget();

            Log.Logger.Verbose("Finished DesignerPane.Initialize()");
        }

        private async Task StartEditorAsync(AvaloniaDesigner designer)
        {
            // Before switching to the main thread, tag the buffer with this pane so that the
            // `XamlErrorTagger` can get hold of the designer process.
            _xmlEditor.TextView.TextBuffer.Properties.AddProperty(
                typeof(DesignerPane),
                this);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Log.Logger.Verbose("Started DesignerPane.StartEditorAsync()");

            var project = ProjectExtensions.GetProjectForFile(_fileName);
            var executablePath = project?.GetAssemblyPath();
            var buffer = _xmlEditor.TextView.TextBuffer;
            var metadata = buffer.Properties.GetProperty<XamlBufferMetadata>(typeof(XamlBufferMetadata));

            if (metadata.CompletionMetadata == null)
            {
                metadata.CompletionMetadata = await CreateCompletionMetadataAsync(executablePath);
            }

            if (executablePath != null)
            {
                Process = new PreviewerProcess(executablePath);
                var xaml = await ReadAllTextAsync(_fileName);
                await Process.StartAsync(xaml);
                designer.Process = Process;
            }

            Initialized?.Invoke(this, EventArgs.Empty);

            Log.Logger.Verbose("Finished DesignerPane.StartEditorAsync()");
        }

        private static async Task<CompletionMetadata> CreateCompletionMetadataAsync(string executablePath)
        {
            await TaskScheduler.Default;

            Log.Logger.Verbose("Started DesignerPane.CreateCompletionMetadataAsync()");

            try
            {
                var metadataReader = new MetadataReader(new DnlibMetadataProvider());
                return metadataReader.GetForTargetAssembly(executablePath);
            }
            catch (Exception ex)
            {
                s_log.Error(ex, "Error creating XAML completion metadata");
                return null;
            }
            finally
            {
                Log.Logger.Verbose("Finished DesignerPane.CreateCompletionMetadataAsync()");
            }
        }

        private static async Task<string> ReadAllTextAsync(string fileName)
        {
            using (var reader = File.OpenText(fileName))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}
