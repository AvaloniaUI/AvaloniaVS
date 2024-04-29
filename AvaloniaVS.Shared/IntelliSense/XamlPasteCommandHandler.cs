using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Avalonia.Ide.CompletionEngine;
using Avalonia.Ide.CompletionEngine.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using IServiceProvider = System.IServiceProvider;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;


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
    internal class XamlPasteCommandHandler : IOleCommandTarget
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOleCommandTarget _nextCommandHandler;
        private readonly IWpfTextView _textView;
        private readonly ITextUndoHistoryRegistry _textUndoHistoryRegistry;
        private readonly CompletionEngine _engine;
        private readonly uint _pasteCommandId = (uint)VSConstants.VSStd97CmdID.Paste;
        public const string Avalonia_DevTools_Selector = nameof(Avalonia_DevTools_Selector);


        public XamlPasteCommandHandler(
            IServiceProvider serviceProvider,
            IWpfTextView textView,
            IVsTextView textViewAdapter,
            ITextUndoHistoryRegistry textUndoHistoryRegistry,
            CompletionEngine completionEngine
            )
        {
            _serviceProvider = serviceProvider;
            _textView = textView;
            _textUndoHistoryRegistry = textUndoHistoryRegistry;
            _engine = completionEngine;

            // Add ourselves as a command to the text view.
            textViewAdapter.AddCommandFilter(this, out _nextCommandHandler);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Clipboard.ContainsData(Avalonia_DevTools_Selector))
            {
                if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
                {
                    for (int i = 0; i < cCmds; i++)
                    {
                        if (prgCmds[i].cmdID == _pasteCommandId)
                        {
                            if (_engine.Helper.Metadata is not null)
                            {
                                var line = _textView.GetTextViewLineContainingBufferPosition(_textView.Caret.Position.BufferPosition);
                                var end = Math.Min(line.End, _textView.Caret.Position.BufferPosition);
                                var parser = XmlParser.Parse(_textView.TextSnapshot.GetText().AsMemory(), 0, end);
                                var state = parser.State;
                                if (state == XmlParser.ParserState.AttributeValue ||
                                    state == XmlParser.ParserState.AfterAttributeValue)
                                {
                                    if (parser.AttributeName?.Equals("Selector") == true)
                                    {
                                        prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                                    }
                                }
                            }
                            return VSConstants.S_OK;
                        }
                    }
                }
            }
            return _nextCommandHandler?.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText) ?? (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // If we're in an automation function, move to the next command.
            if (!VsShellUtilities.IsInAutomationFunction(_serviceProvider) && HandlePasteCommand(pguidCmdGroup, nCmdID))
            {
                return VSConstants.S_OK;
            }
            var result = _nextCommandHandler?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) ?? VSConstants.S_OK;
            return result;
        }

        private bool HandlePasteCommand(Guid pguidCmdGroup, uint nCmdID)
        {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97
                && nCmdID == _pasteCommandId
                )
            {
                if (Clipboard.GetData(Avalonia_DevTools_Selector) is MemoryStream @do)
                {
                    var bytes = @do.ToArray();
                    var seletcorText = System.Text.Encoding.Unicode.GetChars(bytes, 0, bytes.Length - 2);
                    var x = _textView.Selection.SelectedSpans;
                    var p = _textView.Caret.Position.BufferPosition;

                    _ = TranslateSelectorAsync(_textView, _textView.TextSnapshot, p, seletcorText)
                        .ConfigureAwait(false);

                    return true;
                }

            }
            return false;
        }

        private async Task TranslateSelectorAsync(ITextView textView, ITextSnapshot snapshot, int postion, char[] seletcorChars)
        {
            var selectorsInfo = DevToolsSelectorParser.Parse(seletcorChars);

            if (_engine.Helper.Metadata is { } metadata && selectorsInfo.Count > 0)
            {
                var aliases = _engine.Helper.Aliases;
                var sb = new StringBuilder();

                Index index = default;
                Dictionary<string, string> aliasesToAdd = new();
                var aliasFounded = false;
                foreach (var si in selectorsInfo)
                {
                    if (si.AssemblyName.Start.Value - 1 > index.Value)
                    {
                        sb.Append(seletcorChars, index.Value, si.AssemblyName.Start.Value - 1 - index.Value);
                    }
                    if (si.AssemblyName.End.Value > index.Value)
                    {
                        index = si.AssemblyName.End.Value + 1;
                    }
                    sb.Append(seletcorChars[index..si.Namespace.Start]);
                    var fn = DevToolsSelectorInfo.GetFullName(seletcorChars, si);
                    if (metadata.InverseNamespace.TryGetValue(fn, out var namespaces) && namespaces.Length > 0)
                    {
                        aliasFounded = false;
                        foreach (var item in namespaces)
                        {
                            if (aliases.FirstOrDefault((a, arg) => string.Equals(a.Value, arg), item) is { Key: not null } kv)
                            {
                                aliasFounded = true;
                                if (!string.IsNullOrEmpty(kv.Value))
                                {
                                    sb.Append(kv.Key);
                                    sb.Append('|');
                                }
                                break;
                            }
                        }
                        if (aliasFounded == false)
                        {
                            var @namespace = string.Concat(seletcorChars[si.Namespace]);
                            var xmlsns = CompletionEngine.GetXmlnsFromNamespace(seletcorChars[si.Namespace]);
                            aliasesToAdd.Add(xmlsns, @namespace);
                            sb.Append(xmlsns);
                            sb.Append('|');
                        }
                        sb.Append(seletcorChars[si.ElementType]);
                        index = si.ElementType.End.Value;
                    }
                }
                sb.Append(seletcorChars[index..seletcorChars.Length]);

                if (sb.Length > 0)
                {
                    var undoHistory = _textUndoHistoryRegistry.RegisterHistory(textView);
                    using (var transaction = undoHistory.CreateTransaction("Paste Style Selector"))
                    {
                        using (var edit = snapshot.TextBuffer.CreateEdit())
                        {
                            edit.Insert(postion, sb.ToString());

                            if (aliasesToAdd.Count > 0)
                            {
                                int i = 0;
                                for (; edit.Snapshot[i] != '>' && i < edit.Snapshot.Length; i++)
                                {

                                }
                                if (i > 0 && i < edit.Snapshot.Length)
                                {
                                    var b = _textView.FormattedLineSource.BaseIndentation;

                                    var line = edit.Snapshot.GetLineFromPosition(i);
                                    var indentation_Space = CalculateLeftOfFirstChar(line, _textView.FormattedLineSource);
                                    var tabSize = _textView.Options.GetTabSize();
                                    var indentationStyle = _textView.Options.GetIndentStyle();
                                    var newline = _textView.Options.GetNewLineCharacter();
                                    var options = _textView.Options.SupportedOptions.ToArray();
                                    var convert = _textView.Options.IsConvertTabsToSpacesEnabled();

                                    var indentationBuilder = new StringBuilder(indentation_Space);
                                    if (convert)
                                    {
                                        indentationBuilder.Append(' ', indentation_Space);
                                    }
                                    else
                                    {
                                        var ntabs = Math.DivRem(indentation_Space, tabSize, out var remainder);
                                        indentationBuilder.Append('\t', ntabs);
                                        indentationBuilder.Append(' ', remainder);
                                    }

                                    foreach (var item in aliasesToAdd)
                                    {

                                        edit.Insert(i, $"{newline}{indentationBuilder}xmlns:{item.Key}=\"using:{item.Value}\"");
                                    }
                                }
                            }
                            edit.Apply();
                        }
                        if (snapshot != textView.TextSnapshot)
                            transaction.Complete();
                    }

                }
            }

            await Task.CompletedTask;
        }

        private static int CalculateLeftOfFirstChar(ITextSnapshotLine line, IFormattedLineSource fls)
        {
            var nspace = 0;
            var start = line.Start;
            while (start.GetChar() is { } ch && char.IsWhiteSpace(ch))
            {
                if (ch == ' ')
                {
                    nspace++;
                }
                else if (ch == '\t')
                {
                    nspace += fls.TabSize;
                }
                start += 1;
            }
            return nspace;
        }
    }
}
