using System;
using System.ComponentModel.Design;
using System.IO;
using System.Windows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using AvaloniaVS.Internals;
using Constants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace AvaloniaVS.Infrastructure
{
    public partial class AvaloniaDesignerPane : IOleCommandTarget
    {
        private IOleCommandTarget _editorCommandTarget;

        private IOleCommandTarget OleCommandTarget
        {
            get
            {
                return _editorCommandTarget ?? (_editorCommandTarget = (IOleCommandTarget)_vsCodeWindow);
            }
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                if (nCmdID == (int)VSConstants.VSStd97CmdID.NewWindow ||
                    nCmdID == (int)VSConstants.VSStd97CmdID.ViewCode ||
                    nCmdID == (int)VSConstants.VSStd97CmdID.ViewForm)
                {
                    var oleCommandTarget = GetService(typeof(IOleCommandTarget)) as IOleCommandTarget;
                    if (oleCommandTarget != null)
                    {
                        return oleCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                    }
                }
            }

            var hr = (int)Constants.OLECMDERR_E_NOTSUPPORTED;
            if (OleCommandTarget != null)
            {
                hr = OleCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
            return hr;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            var hr = (int)Constants.OLECMDERR_E_NOTSUPPORTED;
            
            // we want the VSConstants.VSStd97CmdID.NewWindow and the VSConstants.VSStd97CmdID.ViewCode to be handled by the 
            // WindowPane rather than the text editor host.
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                if (cCmds == 1 && (prgCmds[0].cmdID == (int)VSConstants.VSStd97CmdID.NewWindow ||
                                   prgCmds[0].cmdID == (int)VSConstants.VSStd97CmdID.ViewCode ||
                                   prgCmds[0].cmdID == (int)VSConstants.VSStd97CmdID.ViewForm))
                {
                    var oleCommandTarget = GetService(typeof(IOleCommandTarget)) as IOleCommandTarget;
                    if (oleCommandTarget != null)
                    {
                        return oleCommandTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
                    }
                }
            }

            if (OleCommandTarget != null)
            {
                hr = OleCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
            }
            return hr;
        }

        protected override bool PreProcessMessage(ref System.Windows.Forms.Message m)
        {
            //Only try and pre-process keyboard input messages, all others are not interesting to us.
            if (m.Msg >= WM_KEYFIRST && m.Msg <= WM_KEYLAST)
            {
                //Only attempt to do the input -> command mapping if focus is inside our hosted editor.
                if (_designerHost.EditView.IsKeyboardFocusWithin)
                {
                    IVsFilterKeys2 filterKeys = (IVsFilterKeys2)GetService(typeof(SVsFilterKeys));
                    MSG oleMSG = new MSG() { hwnd = m.HWnd, lParam = m.LParam, wParam = m.WParam, message = (uint)m.Msg };

                    //Ask the shell to do the command mapping for us and fire off the command if it succeeds with that mapping. We pass no 'custom' scopes
                    //(third and fourth argument) because we pass VSTAEXF_UseTextEditorKBScope to indicate we want the shell to apply the text editor
                    //command scope to this call.
                    Guid cmdGuid;
                    uint cmdId;
                    int fTranslated;
                    int fStartsMultiKeyChord;
                    int res = filterKeys.TranslateAcceleratorEx(new MSG[] { oleMSG },
                                                                (uint)(__VSTRANSACCELEXFLAGS.VSTAEXF_UseTextEditorKBScope),
                                                                0 /*scope count*/,
                                                                new Guid[0] /*scopes*/,
                                                                out cmdGuid,
                                                                out cmdId,
                                                                out fTranslated,
                                                                out fStartsMultiKeyChord);

                    if (fStartsMultiKeyChord == 0)
                    {
                        //HACK: Work around a bug in TranslateAcceleratorEx that will report it DIDN'T do the command mapping 
                        //when in fact it did :( Problem has been fixed (since I found it while writing this code), but in the 
                        //mean time we need to successfully eat keystrokes that have been mapped to commands and dispatched, 
                        //we DON'T want them to continue on to Translate/Dispatch. "Luckily" asking TranslateAcceleratorEx to
                        //do the mapping WITHOUT firing the command will give us the right result code to indicate if the command
                        //mapped or not, unfortunately we can't always do this as it would break key-chords as it causes the shell 
                        //to not remember the first input match of a multi-part chord, hence the reason we ONLY hit this block if 
                        //it didn't tell us the input IS part of key-chord.
                        res = filterKeys.TranslateAcceleratorEx(new MSG[] { oleMSG },
                                                                (uint)(__VSTRANSACCELEXFLAGS.VSTAEXF_NoFireCommand | __VSTRANSACCELEXFLAGS.VSTAEXF_UseTextEditorKBScope),
                                                                0,
                                                                new Guid[0],
                                                                out cmdGuid,
                                                                out cmdId,
                                                                out fTranslated,
                                                                out fStartsMultiKeyChord);

                        return res == VSConstants.S_OK;
                    }

                    //We return true (that we handled the input message) if we managed to map it to a command OR it was the 
                    //beginning of a multi-key chord, anything else should continue on with normal processing.
                    return (res == VSConstants.S_OK) || (fStartsMultiKeyChord != 0);
                }
            }

            return base.PreProcessMessage(ref m);
        }

        private void RegisterMenuCommands()
        {
            var menuService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (menuService != null)
            {
                // Window->New Window Command
                AddCommand(menuService, VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.NewWindow, OnNewWindow, OnNewWindowBeforeQueryStatus);
                AddCommand(menuService, VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.ViewCode, OnViewCode, OnViewCodeQueryStatus);
                AddCommand(menuService, VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.ViewForm, OnViewForm, OnViewFormQueryStatus);
            }
        }

        private void OnViewFormQueryStatus(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;
            command.Enabled = true;
        }

        private void OnViewForm(object sender, EventArgs e)
        {
            _designerHost.Container.SwapActiveView();
            if (_designerHost.Container.IsEditorActive)
            {
                IVsTextView lastActiveView;
                GetLastActiveView(out lastActiveView);

                var wpfEditorView = lastActiveView as IWpfTextView;
                if (wpfEditorView != null)
                {
                    wpfEditorView.VisualElement.Focus();
                }
            }
        }

        private void OnViewCodeQueryStatus(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;
            command.Enabled = true;
        }

        private void OnViewCode(object sender, EventArgs e)
        {
            var codeBehindFile = $"{_fileName}.cs";
            if (!File.Exists(codeBehindFile))
            {
                return;
            }

            VsShellUtilities.OpenDocument(this, codeBehindFile);
        }

        private void OnNewWindowBeforeQueryStatus(object sender, EventArgs eventArgs)
        {
            var newWindowCommand = sender as OleMenuCommand;
            if (newWindowCommand != null)
            {
                newWindowCommand.Visible = false;
            }
        }

        private void OnNewWindow(object sender, EventArgs eventArgs) { }

        private static void AddCommand(IMenuCommandService mcs, Guid menuGroup, int cmdId, EventHandler invokeHandler, EventHandler beforeQueryStatus)
        {
            var id = new CommandID(menuGroup, cmdId);
            var command = new OleMenuCommand(invokeHandler, id) { Visible = true };

            if (beforeQueryStatus != null)
            {
                command.BeforeQueryStatus += beforeQueryStatus;
            }

            mcs.AddCommand(command);
        }
    }
}
