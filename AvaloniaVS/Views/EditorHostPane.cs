using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace AvaloniaVS.Views
{
    /// <summary>
    /// A <see cref="WindowPane"/> which hosts a VS editor control.
    /// </summary>
    /// <remarks>
    /// This class extends <see cref="WindowPane"/> to implement <see cref="IOleCommandTarget"/>
    /// and implements the required plumbing to forward commands to the hosted editor.
    /// </remarks>
    public abstract class EditorHostPane : WindowPane, IOleCommandTarget
    {
        private const int WM_KEYFIRST = 0x0100;
        private const int WM_KEYLAST = 0x0109;

        private readonly IVsCodeWindow _editorWindow;
        private readonly IOleCommandTarget _editorCommandTarget;
        private IVsFilterKeys2 _filterKeys;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorHostPane"/> class;
        /// </summary>
        /// <param name="editorWindow">The editor window to host.</param>
        public EditorHostPane(IVsCodeWindow editorWindow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _editorWindow = editorWindow ?? throw new ArgumentNullException(nameof(editorWindow));
            _editorCommandTarget = (IOleCommandTarget)editorWindow;
        }

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _editorCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _editorCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        protected override bool PreProcessMessage(ref System.Windows.Forms.Message m)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (m.Msg >= WM_KEYFIRST && m.Msg <= WM_KEYLAST)
            {
                var oleMsg = new MSG
                {
                    hwnd = m.HWnd,
                    lParam = m.LParam,
                    wParam = m.WParam,
                    message = (uint)m.Msg
                };

                if (_filterKeys == null)
                {
                    _filterKeys = this.GetService<IVsFilterKeys2, SVsFilterKeys>();
                }

                return _filterKeys.TranslateAcceleratorEx(
                    new[] { oleMsg },
                    (uint)__VSTRANSACCELEXFLAGS.VSTAEXF_UseTextEditorKBScope,
                    0,
                    Array.Empty<Guid>(),
                    out var _,
                    out var _,
                    out var _,
                    out var _) == VSConstants.S_OK;
            }

            return base.PreProcessMessage(ref m);
        }
    }
}
