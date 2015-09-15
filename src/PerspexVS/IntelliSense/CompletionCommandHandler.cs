using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace PerspexVS.IntelliSense
{
    [Export(typeof (IVsTextViewCreationListener))]
    [Name("Perspex Completion Handler")]
    [ContentType("xml")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class CompletionHandlerProvider : IVsTextViewCreationListener
    {
        [Import] internal IVsEditorAdaptersFactoryService AdapterService = null;

        [Import]
        internal ICompletionBroker CompletionBroker { get; set; }

        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);
            if (textView == null)
                return;

            Func<CompletionCommandHandler> createCommandHandler =
                () => new CompletionCommandHandler(textViewAdapter, textView, this);
            textView.Properties.GetOrCreateSingletonProperty(createCommandHandler);
        }
    }

    internal class CompletionCommandHandler : IOleCommandTarget
    {
        private readonly IOleCommandTarget _nextCommandHandler;
        private readonly IOleCommandTarget _realCommandHandler;
        private readonly ITextView _textView;
        private readonly CompletionHandlerProvider _provider;
        private ICompletionSession _session;


        internal CompletionCommandHandler(IVsTextView textViewAdapter, ITextView textView,
            CompletionHandlerProvider provider)
        {
            this._textView = textView;
            this._provider = provider;

            //add the command to the command chain
            textViewAdapter.AddCommandFilter(this, out _nextCommandHandler);
            var nextProp = _nextCommandHandler.GetType().GetProperty("Next");
            var filterObjectProp = _nextCommandHandler.GetType().GetProperty("FilterObject");

            _realCommandHandler =
                (IOleCommandTarget)
                    filterObjectProp.GetValue(nextProp.GetValue(nextProp.GetValue(_nextCommandHandler)));
            textView.Properties[typeof (CompletionCommandHandler)] = this;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            return _nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (VsShellUtilities.IsInAutomationFunction(_provider.ServiceProvider))
            {
                return _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }


            //make a copy of this so we can look at it after forwarding some commands 
            uint commandID = nCmdID;
            char typedChar = char.MinValue;
            //make sure the input is a char before getting it 
            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint) VSConstants.VSStd2KCmdID.TYPECHAR)
            {
                typedChar = (char) (ushort) Marshal.GetObjectForNativeVariant(pvaIn);
            }

            //check for a commit character 
            if (nCmdID == (uint) VSConstants.VSStd2KCmdID.RETURN
                || nCmdID == (uint) VSConstants.VSStd2KCmdID.TAB
                || (char.IsWhiteSpace(typedChar) || char.IsPunctuation(typedChar)))
            {
                //check for a a selection 
                if (_session != null && !_session.IsDismissed)
                {
                    //if the selection is fully selected, commit the current session 
                    if (_session.SelectedCompletionSet.SelectionStatus.IsSelected)
                    {
                        _session.Commit();
                        //also, don't add the character to the buffer 
                        return VSConstants.S_OK;
                    }
                    else
                    {
                        //if there is no selection, dismiss the session
                        _session.Dismiss();
                    }
                }
            }

            //pass along the command so the char is added to the buffer 
            int retVal = VSConstants.S_OK;
            bool handled = false;
            if (nCmdID == (uint) VSConstants.VSStd2KCmdID.COMPLETEWORD ||
                (!typedChar.Equals(char.MinValue) &&
                 (char.IsLetterOrDigit(typedChar) || typedChar == '<' || typedChar == ' ' || typedChar  == '.')))
            {
                if (typedChar != '\0')
                    retVal = _realCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                if (_session == null || _session.IsDismissed)
                    // If there is no active session, bring up completion
                {

                    TriggerCompletion();
                    if (typedChar != '<' && _session != null)
                        _session.Filter();
                }
                else
                {
                    _session.Filter();
                }
                handled = true;
            }
            else if (commandID == (uint) VSConstants.VSStd2KCmdID.BACKSPACE //redo the filter if there is a deletion
                     || commandID == (uint) VSConstants.VSStd2KCmdID.DELETE)
            {
                if (_session != null && !_session.IsDismissed)
                    _session.Filter();
                retVal = _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                handled = true;
            }
            else
                retVal = _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            if (handled) return VSConstants.S_OK;
            return retVal;
        }

        public void TriggerNew()
        {
            if (_session != null)
                OnSessionDismissed(null, null);
            TriggerCompletion();
        }

        public bool TriggerCompletion()
        {
            //the caret must be in a non-projection location 
            SnapshotPoint? caretPoint =
                _textView.Caret.Position.Point.GetPoint(
                    textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);
            if (!caretPoint.HasValue)
            {
                return false;
            }

            _session = _provider.CompletionBroker.CreateCompletionSession
                (_textView,
                    caretPoint.Value.Snapshot.CreateTrackingPoint(caretPoint.Value.Position,
                        PointTrackingMode.Positive),
                    true);

            //subscribe to the Dismissed event on the session 
            _session.Dismissed += this.OnSessionDismissed;
            _session.Start();

            return true;
        }

        private void OnSessionDismissed(object sender, EventArgs e)
        {
            _session.Dismissed -= this.OnSessionDismissed;
            _session = null;
        }
    }
}
