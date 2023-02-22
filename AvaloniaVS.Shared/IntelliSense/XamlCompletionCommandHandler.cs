using System;
using System.Runtime.InteropServices;
using Avalonia.Ide.CompletionEngine;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using IServiceProvider = System.IServiceProvider;

namespace AvaloniaVS.IntelliSense
{
    /// <summary>
    /// Handles key presses for the Avalonia XAML intellisense completion.
    /// </summary>
    /// <remarks>
    /// Adds a command handler to text views and listens for keypresses which should cause a
    /// completion to be opened or comitted.
    /// 
    /// Yes, this is horrible, but it's apparently the official way to do this. Eurgh.
    /// </remarks>
    internal class XamlCompletionCommandHandler : IOleCommandTarget
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ICompletionBroker _completionBroker;
        private readonly IOleCommandTarget _nextCommandHandler;
        private readonly ITextView _textView;
        private ICompletionSession _session;

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

            if (TryGetChar(ref pguidCmdGroup, nCmdID, pvaIn, out var c))
            {
                if (HandleSessionCompletion(c, out var newSession))
                {
                    return VSConstants.S_OK;
                }

                if (_session == null && (c == '\'' || c == '"'))
                {
                    // If a completion session isn't active, and we type a quote, check
                    // if a quote already exists at the position & just move the cursor
                    // so we don't get a double quote
                    var cursorPos = _textView.Caret.Position.BufferPosition;
                    var nextChar = _textView.TextSnapshot.GetText(cursorPos, 1)[0];
                    if (nextChar == c)
                    {
                        _textView.Caret.MoveTo(cursorPos + 1);
                        return VSConstants.S_OK;
                    }
                }

                var result = _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

                if (HandleSessionStart(c, newSession))
                {
                    return VSConstants.S_OK;
                }

                if (HandleSessionUpdate(c))
                {
                    return VSConstants.S_OK;
                }
                
                return result;
            }

            return _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private bool HandleSessionStart(char c, bool forceNewSession)
        {
            //if (forceNewSession)
            //{
            //    TriggerCompletion();
            //    return true;
            //}

            // If the pressed key is a key that can start a completion session.
            if (CompletionEngine.ShouldTriggerCompletionListOn(c) || c == '\a')
            {
                if (_session == null || _session.IsDismissed)
                {
                    if (TriggerCompletion() && c != '<' && c != '.' && c != ' ')
                    {
                        _session?.Filter();
                    }

                    return true;
                }
                else
                {
                    // Special case for pseudoclasses - since they don't have spaces between
                    // them and the element before (Control:pointerover) we need to cancel
                    // the previous completion session and start a new one starting at the ':'
                    // Otherwise typing 'Control:' won't show the intellisense popup with the
                    // pseudoclasses until after you start typing a pseudoclass
                    if (c == ':')// || c == '.')
                    {
                        // But, we don't want to trigger a new session for the ':' char if we're
                        // not in a Selector. Otherwise, we'll trigger a new session for something
                        // like 'xmlns:' or 'xmlsn:ui="using:' or '<ui:' (for third party controls)
                        // which causes the intellisense popup to temporarily disappear and we don't
                        // want that
                        if (c == ':')
                        {
                            var pos = _textView.Caret.Position;
                            var state = XmlParser.Parse(_textView.TextSnapshot.GetText().AsMemory(),
                                0, pos.BufferPosition.Position);

                            if (!(state?.AttributeName?.Equals("Selector") == true))
                            {
                                _session?.Filter();
                                return false;
                            }
                        }

                        _session.Dismiss();
                        return false;
                    }

                    //_session.Filter();
                }
            }

            return false;
        }

        private bool HandleSessionUpdate(char c)
        {
            // Update the filter if there is a deletion.
            if (c == '\b')
            {
                if (_session != null && !_session.IsDismissed)
                {
                    _session.Filter();
                }

                return true;
            }

            return false;
        }

        private bool HandleSessionCompletion(char c, out bool shouldTriggerSession)
        {
            shouldTriggerSession = false;
            var line = _textView.GetTextViewLineContainingBufferPosition(
                _textView.Caret.Position.BufferPosition);
            var start = line.Start;
            var end = Math.Min(line.End, _textView.Caret.Position.BufferPosition);
            var text = line.Snapshot.GetText(start, end-start);
            
            // Adding a xmlns is special-cased here because we don't want '.' triggering
            // a completion, which can complete on the wrong value
            // So we only trigger on ' ' or '\t', and swallow that so it doesn't get 
            // inserted into the text buffer
            if (_session != null && !_session.IsDismissed)
            {
                if (text.Contains("xmlns"))
                {
                    if (char.IsWhiteSpace(c))
                    {
                        _session.Commit();
                        return true;
                    }

                    return false;
                }
            }

            // Per UWP designer, the following keys can commit a completion session
            // in the remainder of the document - but only if a completion option
            // is selected
            // ' ' (space, or tab) 
            // '\'' (single quote)
            // '"'
            // '='
            // '>'
            // '.'

            // Also adding '#' for Selectors

            if (char.IsWhiteSpace(c) || c == '\'' || c == '"' || c == '=' || c == '>' || c == '.' || c == '#')
            {
                if (_session != null && !_session.IsDismissed &&
                    _session.SelectedCompletionSet.SelectionStatus.IsSelected)
                {
                    var selected = _session.SelectedCompletionSet.SelectionStatus.Completion as XamlCompletion;

                    _session.Commit();
                    if (selected?.CursorOffset > 0)
                    {
                        // Offset the cursor if necessary e.g. to place it within the quotation
                        // marks of an attribute.
                        var cursorPos = _textView.Caret.Position.BufferPosition;
                        var newCursorPos = cursorPos - selected.CursorOffset;
                        _textView.Caret.MoveTo(newCursorPos);
                    }

                    // For perf, we only parse the text of the current line - if the 'state' comes
                    // back as None, its an Xml Attribute since xml can't "wrap" lines and if no xml 
                    // element/tag is found the only possible thing it can be is an attribute -
                    // at least in terms of intellisense
                    var parser = XmlParser.Parse(_textView.TextSnapshot.GetText().AsMemory(), 0, end);
                    var state = parser.State;

                    bool skip = c != '>';
                    if (state == XmlParser.ParserState.StartElement && 
                        (c == '.' || c == ' '))
                    {
                        // Don't swallow the '.' or ' ' if this is an Xml element, like
                        // Window.Resources. However do swallow tab
                        
                        skip = false;
                    }

                    if (state == XmlParser.ParserState.AttributeValue || 
                        state == XmlParser.ParserState.AfterAttributeValue)
                    {
                        if (char.IsWhiteSpace(c) && c != ' ')
                        {
                            skip = true;
                        }
                        else if (c == '\'' || c == '"')
                        {
                            // If we're accepting a completion using the quotes, and there's already one
                            // in the buffer after the completion, don't insert another quote, swallow
                            // it and just move the cursor
                            var cursorPos = _textView.Caret.Position.BufferPosition;
                            var nextChar = _textView.TextSnapshot.GetText(cursorPos, 1)[0];
                            if (nextChar == c)
                            {
                                skip = true;
                                _textView.Caret.MoveTo(cursorPos + 1);
                            }
                        }
                        else
                        {
                            skip = false;
                        }                        
                    }
                    else if (state != XmlParser.ParserState.StartElement)
                    {
                        TriggerCompletion();
                    }
                    

                    shouldTriggerSession = true;
                    return skip;
                }
            }
            else if (c == ':' && (_session != null && !_session.IsDismissed))
            {
                _session.Dismiss();
                return false;
            }



                       // If the pressed key is a key that can commit a completion session.
            if (false || char.IsWhiteSpace(c) ||
                (char.IsPunctuation(c) && c != ':' && c != '/' && c != '-') ||
                c == '\n' || c == '\r' || c == '=')
            {
                // And commit or dismiss the completion session depending its state.
                if (_session != null && !_session.IsDismissed)
                {
                    if (_session.SelectedCompletionSet.SelectionStatus.IsSelected)
                    {
                        var selected = _session.SelectedCompletionSet.SelectionStatus.Completion as XamlCompletion;

                        // If the spacebar is used to complete then it should be entered into the
                        // buffer, all other chars should be swallowed.
                        // Don't swallow '.' either, otherwise it will require two presses of the '.' key
                        // for something like 'Window.Resources'
                        var skip = c != ' ' && c != '.';

                        //_session.Commit();

                        if (selected?.CursorOffset > 0)
                        {
                            // Offset the cursor if necessary e.g. to place it within the quotation
                            // marks of an attribute.
                            var cursorPos = _textView.Caret.Position.BufferPosition;
                            var newCursorPos = cursorPos - selected.CursorOffset;
                            _textView.Caret.MoveTo(newCursorPos);
                        }

                        // If the inserted text is an XML attribute or attached property then pop up a new completion
                        // session to show the valid values for the attribute.
                        if (selected.InsertionText.EndsWith("=\"\"") || selected.InsertionText.EndsWith("."))
                        {
                            TriggerCompletion();
                        }

                        //return skip;
                    }
                    else
                    {
                        //_session.Dismiss();
                    }
                }
            }

            return false;
        }

        private bool TriggerCompletion()
        {
            // The caret must be in a non-projection location.
            var caretPoint = _textView.Caret.Position.Point.GetPoint(
                x => (!x.ContentType.IsOfType("projection")),
                PositionAffinity.Predecessor);

            if (!caretPoint.HasValue)
            {
                return false;
            }

            // When adding an xmlns definition, we were getting 2 intellisense popups because (I think)
            // the VS XML intellisense handler was popping one up and then we are creating our own session
            // here. It turns out one of the completionsets though is an Avalonia one, so if a session already
            // exists and one of the CompletionSets is from Avalonia, use that session instead of creating
            // a new one - and we won't get the double popup
            ICompletionSession existingSession = null;
            var sessions = _completionBroker.GetSessions(_textView);
            if (sessions.Count > 0)
            {
                for (int i = sessions.Count - 1; i >= 0; i--)
                {
                    if (sessions[i].CompletionSets.Count == 0)
                        sessions[i].Dismiss();

                    var sets = sessions[i].CompletionSets;

                    for (int j = sets.Count - 1; j >= 0; j--)
                    {
                        if (sets[j].Moniker.Equals("Avalonia"))
                        {
                            existingSession = sessions[i];
                            break;
                        }
                    }

                    if (existingSession != null)
                        break;
                }
            }

            _session = existingSession ?? _completionBroker.CreateCompletionSession(
                _textView,
                caretPoint?.Snapshot.CreateTrackingPoint(caretPoint.Value.Position, PointTrackingMode.Positive),
                true);

            // Subscribe to the Dismissed event on the session.
            _session.Dismissed += SessionDismissed;
            _session.Start();

            return true;
        }

        private void SessionDismissed(object sender, EventArgs e)
        {
            _session.Dismissed -= SessionDismissed;
            _session = null;
        }

        private static bool TryGetChar(ref Guid pguidCmdGroup, uint nCmdID, IntPtr pvaIn, out char c)
        {
            c = '\0';

            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
                    case VSConstants.VSStd2KCmdID.TYPECHAR:
                        c = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                        break;
                    case VSConstants.VSStd2KCmdID.RETURN:
                        c = '\n';
                        break;
                    case VSConstants.VSStd2KCmdID.TAB:
                        c = '\t';
                        break;
                    case VSConstants.VSStd2KCmdID.BACKSPACE:
                    case VSConstants.VSStd2KCmdID.DELETE:
                        c = '\b';
                        break;
                    // Translate Ctrl+Space into a '\a'.
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        c = ' ';
                        break;
                }
            }

            return c != '\0';
        }
    }
}
