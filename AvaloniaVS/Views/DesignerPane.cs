using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;
using AvaloniaVS.Models;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using CompletionMetadata = Avalonia.Ide.CompletionEngine.Metadata;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS.Views
{
    public class DesignerPane : EditorHostPane
    {
        private readonly string _fileName;
        private readonly IWpfTextViewHost _xmlEditor;
        private AvaloniaDesigner _content;
        private PreviewerProcess _process;

        public DesignerPane(string fileName, IVsCodeWindow xmlEditorWindow, IWpfTextViewHost xmlEditor)
            : base(xmlEditorWindow)
        {
            _fileName = fileName;
            _xmlEditor = xmlEditor;
        }

        public override object Content => _content;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _content?.Dispose();
            _content = null;
            _process?.Dispose();
            _process = null;
        }

        protected override void Initialize()
        {
            base.Initialize();

            var xamlEditorView = new AvaloniaDesigner
            {
                XmlEditor = _xmlEditor,
            };

            _content = xamlEditorView;
            StartEditorAsync(xamlEditorView).FireAndForget();
        }

        private async Task StartEditorAsync(AvaloniaDesigner editor)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var project = ProjectExtensions.GetProjectForFile(_fileName);
            var executablePath = project?.GetAssemblyPath();
            var buffer = _xmlEditor.TextView.TextBuffer;
            var metadata = buffer.Properties.GetOrCreateSingletonProperty(
                typeof(XamlBufferMetadata),
                () => new XamlBufferMetadata());

            if (metadata.CompletionMetadata == null)
            {
                metadata.CompletionMetadata = await CreateCompletionMetadataAsync(executablePath);
            }

            if (executablePath != null)
            {
                var xaml = await ReadAllTextAsync(_fileName);
                _process = new PreviewerProcess(executablePath);
                await _process.StartAsync(xaml);
                editor.Process = _process;
            }
        }

        private static async Task<CompletionMetadata> CreateCompletionMetadataAsync(string executablePath)
        {
            await TaskScheduler.Default;

            try
            {
                var metadataReader = new MetadataReader(new DnlibMetadataProvider());
                return metadataReader.GetForTargetAssembly(executablePath);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error creating XAML completion metadata: " + e);
                return null;
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
