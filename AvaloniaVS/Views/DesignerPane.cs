using System;
using System.IO;
using System.Threading.Tasks;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS.Views
{
    public class DesignerPane : WindowPane, IDisposable
    {
        private readonly string _fileName;
        private XamlEditorView _content;
        private PreviewerProcess _process;

        public DesignerPane(string fileName)
        {
            _fileName = fileName;
        }

        public override object Content => _content;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _process?.Dispose();
            _process = null;
        }

        protected override void Initialize()
        {
            base.Initialize();

            var editor = new XamlEditorView();
            _content = editor;
            StartEditorAsync(editor).FireAndForget();
        }

        private async Task StartEditorAsync(XamlEditorView editor)
        {
            var project = ProjectExtensions.GetProjectForFile(_fileName);
            var executablePath = project?.GetAssemblyPath();

            if (executablePath != null)
            {
                var xaml = await ReadAllTextAsync(_fileName);

                _process = new PreviewerProcess(executablePath);
                editor.Process = _process;
                _process.Start(xaml);
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
