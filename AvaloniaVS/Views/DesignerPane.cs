using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
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

            _process?.Dispose();
            _process = null;
            
            if (Content is AvaloniaDesigner view)
            {
                view.XmlEditor = null;
            }
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

            if (executablePath != null)
            {
                var xaml = await ReadAllTextAsync(_fileName);

                _process = new PreviewerProcess(executablePath);
                await _process.StartAsync(xaml);
                editor.Process = _process;
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
