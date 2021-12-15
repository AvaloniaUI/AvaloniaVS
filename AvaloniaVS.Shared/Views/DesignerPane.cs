using AvaloniaVS.Services;
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
        private readonly DTEEvents _dteEvents;
        private readonly BuildEvents _buildEvents;
        private AvaloniaDesigner _content;
        private bool _isPaused;

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
            ThreadHelper.ThrowIfNotOnUIThread();

            _project = project;
            _xamlPath = xamlPath;
            _editorHost = editorHost;

            var dte = (DTE)Package.GetGlobalService(typeof(DTE));
            _buildEvents = dte.Events.BuildEvents;
            _dteEvents = dte.Events.DTEEvents;
            _isPaused = dte.Mode == vsIDEMode.vsIDEModeDebug;

            _buildEvents.OnBuildBegin += HandleBuildBegin;
            _buildEvents.OnBuildDone += HandleBuildDone;
            _dteEvents.ModeChanged += HandleModeChanged;
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

            var settings = this.GetMefService<IAvaloniaVSSettings>();
            var xamlEditorView = new AvaloniaDesigner();
            xamlEditorView.IsPaused = _isPaused;
            xamlEditorView.SplitOrientation = settings.DesignerSplitOrientation;
            xamlEditorView.View = settings.DesignerView;
            xamlEditorView.Start(_project, _xamlPath, _editorHost);
            _content = xamlEditorView;

            Log.Logger.Verbose("Finished DesignerPane.Initialize()");
        }

        private void HandleModeChanged(vsIDEMode lastMode)
        {
            if (_content != null)
            {
                _content.IsPaused = _isPaused = lastMode == vsIDEMode.vsIDEModeDesign;
            }
        }

        private void HandleBuildBegin(vsBuildScope Scope, vsBuildAction Action)
        {
            Log.Logger.Debug("Build started");

            _isPaused = true;

            if (_content != null)
            {
                _content.IsPaused = _isPaused;
            }
        }

        private void HandleBuildDone(vsBuildScope Scope, vsBuildAction Action)
        {
            Log.Logger.Debug("Build finished");

            _isPaused = false;

            if (_content != null)
            {
                _content.InvalidateCompletionMetadata();
                _content.IsPaused = _isPaused;
            }
        }
    }
}
