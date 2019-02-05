using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Serilog;

namespace AvaloniaVS.Views
{
    /// <summary>
    /// A <see cref="WindowPane"/> used to host an <see cref="AvaloniaDesigner"/>.
    /// </summary>
    internal class DesignerPane : EditorHostPane
    {
        private readonly Project _project;
        private readonly string _xamlPath;
        private readonly IWpfTextViewHost _editorHost;
        private AvaloniaDesigner _content;

        /// <summary>
        /// Initializes a new instance of the <see cref="DesignerPane"/> class.
        /// </summary>
        /// <param name="project">The project containing the XAML file to edit.</param>
        /// <param name="xamlPath">The path to the XAML file to edit.</param>
        /// <param name="editorWindow">The editor window to be used by the designer.</param>
        /// <param name="editorHost">The editor control to be used by the designer.</param>
        public DesignerPane(
            Project project,
            string xamlPath,
            IVsCodeWindow editorWindow,
            IWpfTextViewHost editorHost)
            : base(editorWindow)
        {
            _project = project;
            _xamlPath = xamlPath;
            _editorHost = editorHost;
        }

        /// <summary>
        /// Gets the content of the window pane.
        /// </summary>
        public override object Content => _content;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _content?.Dispose();
            _content = null;
        }

        protected override void Initialize()
        {
            Log.Logger.Verbose("Started DesignerPane.Initialize()");

            base.Initialize();

            var xamlEditorView = new AvaloniaDesigner();
            xamlEditorView.Start(_project, _xamlPath, _editorHost);
            _content = xamlEditorView;

            Log.Logger.Verbose("Finished DesignerPane.Initialize()");
        }
    }
}
