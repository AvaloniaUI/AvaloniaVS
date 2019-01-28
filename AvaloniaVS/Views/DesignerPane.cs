using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS.Views
{
    public class DesignerPane : EditorHostPane
    {
        private readonly string _fileName;
        private readonly Control _xmlEditor;
        private XamlEditorView _content;
        private PreviewerProcess _process;

        public DesignerPane(string fileName, IVsCodeWindow xmlEditorWindow, Control xmlEditor)
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
        }

        protected override void Initialize()
        {
            base.Initialize();

            var xamlEditorView = new XamlEditorView
            {
                XmlEditor = _xmlEditor,
            };

            _content = xamlEditorView;
            StartEditorAsync(xamlEditorView).FireAndForget();
        }

        private async Task StartEditorAsync(XamlEditorView editor)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
