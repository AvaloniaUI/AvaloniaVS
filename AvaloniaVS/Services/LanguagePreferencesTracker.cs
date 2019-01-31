using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace AvaloniaVS.Services
{
    internal class LanguagePreferencesTracker : IVsTextManagerEvents
    {
        private readonly IVsTextManager _textManager;
        private readonly Guid _language;
        private readonly List<ITextView> _textViews = new List<ITextView>();
        private readonly int _cookie;
        private LANGPREFERENCES _prefs;

        public LanguagePreferencesTracker(IVsTextManager textManager, Guid language)
        {
            _textManager = textManager;
            _language = language;

            _prefs = new LANGPREFERENCES { guidLang = language };
            var array = new[] { _prefs };
            ErrorHandler.ThrowOnFailure(textManager.GetUserPreferences(
                null,
                null,
                array,
                null));
            _prefs = array[0];

            var container = (IConnectionPointContainer)_textManager;
            container.FindConnectionPoint(typeof(IVsTextManagerEvents).GUID, out var connection);
            connection.Advise(this, out _cookie);
        }

        public void Track(ITextView textView)
        {
            _textViews.Add(textView);
            Update(textView.Options);
            textView.Closed += TextViewClosed;
        }

        void IVsTextManagerEvents.OnRegisterMarkerType(int iMarkerType)
        {
        }

        void IVsTextManagerEvents.OnRegisterView(IVsTextView pView)
        {
        }

        void IVsTextManagerEvents.OnUnregisterView(IVsTextView pView)
        {
        }

        void IVsTextManagerEvents.OnUserPreferencesChanged(VIEWPREFERENCES[] pViewPrefs, FRAMEPREFERENCES[] pFramePrefs, LANGPREFERENCES[] pLangPrefs, FONTCOLORPREFERENCES[] pColorPrefs)
        {
            if (pLangPrefs?.Length > 0 && pLangPrefs[0].guidLang == _language)
            {
                _prefs = pLangPrefs[0];

                foreach (var textView in _textViews)
                {
                    Update(textView.Options);
                }
            }
        }

        private void Update(IEditorOptions options)
        {
            options.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, _prefs.fInsertTabs == 0);
            options.SetOptionValue(DefaultOptions.IndentSizeOptionId, (int)_prefs.uIndentSize);
            options.SetOptionValue(DefaultOptions.TabSizeOptionId, (int)_prefs.uTabSize);
            options.SetOptionValue(DefaultTextViewOptions.UseVirtualSpaceId, _prefs.fVirtualSpace > 0);
        }

        private void TextViewClosed(object sender, EventArgs e)
        {
            var textView = (ITextView)sender;
            _textViews.Remove(textView);
            textView.Closed -= TextViewClosed;
        }
    }
}
