using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using IServiceProvider = System.IServiceProvider;

namespace AvaloniaVS.IntelliSense
{
    /// <summary>
    /// Handles key presses for the Avalonia XAML intellisense completion.
    /// </summary>
    /// <remarks>
    /// We rely on the XAML language service to initiate intellisense completion on XAML files,
    /// but unfortunately that service ignores completion sets that it didn't add itself. This
    /// class adds a command handler to text views and listens for keypresses which should cause
    /// a completion to be selected.
    /// 
    /// Yes, this is horrible, but it's apparently the official way to do this. Eurgh.
    /// </remarks>
    internal class XamlCompletionCommandHandler : IOleCommandTarget
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ICompletionBroker _completionBroker;
        private readonly IOleCommandTarget _nextCommandHandler;
        private readonly ITextView _textView;

        public XamlCompletionCommandHandler(
            IServiceProvider serviceProvider,
            ICompletionBroker completionBroker,
            ITextView textView,
            IVsTextView textViewAdapter)
        {
            _serviceProvider = serviceProvider;
            _completionBroker = completionBroker;
            _textView = textView;

            // Add ourselves as a command to the text view.
            textViewAdapter.AddCommandFilter(this, out _nextCommandHandler);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // If we're in an automation function, move to the next command.
            if (VsShellUtilities.IsInAutomationFunction(_serviceProvider))
            {
                return _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }

            // Get the character if the command represents a typed character.
            char typedChar = char.MinValue;

            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
            {
                typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
            }

            // If the pressed key is a key that can commit a completion session.
            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN ||
                nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB || 
                char.IsWhiteSpace(typedChar) ||
                char.IsPunctuation(typedChar) ||
                typedChar == '=' ||
                typedChar == '/')
            {
                // Get the completion session for the text view, if any. Note that we didn't start
                // the completion session: the XAML language service did. However that service
                // ignores keypresses for our completion sets, so we need to implement that ourselves.
                var session = _completionBroker.GetSessions(_textView).FirstOrDefault();

                // And commit or dismiss the completion session depending its state.
                if (session != null && !session.IsDismissed)
                {
                    if (session.SelectedCompletionSet.SelectionStatus.IsSelected)
                    {
                        var selected = session.SelectedCompletionSet.SelectionStatus.Completion as XamlCompletion;

                        session.Commit();

                        if (typedChar == '/' && selected?.IsClass == true)
                        {
                            // If '/' was typed and this is an element, add the closing "'/>".
                            _textView.TextBuffer.Insert(
                                _textView.Caret.Position.BufferPosition.Position,
                                "/>");
                        }
                        else if (selected?.CursorOffset > 0)
                        {
                            // Offset the cursor if necessary e.g. to place it within the quotation
                            // marks of an attribute.
                            var cursorPos = _textView.Caret.Position.BufferPosition;
                            var newCursorPos = cursorPos - selected.CursorOffset;
                            _textView.Caret.MoveTo(newCursorPos);
                        }

                        // Don't add the character to the buffer.
                        return VSConstants.S_OK;
                    }
                    else
                    {
                        session.Dismiss();
                    }
                }
            }

            return _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }
    }
}
