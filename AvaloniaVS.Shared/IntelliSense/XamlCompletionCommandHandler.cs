using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Ide.CompletionEngine;
using EnvDTE;
using EnvDTE80;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
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
        private readonly CompletionEngine _engine;
        private ICompletionSession _session;

        public XamlCompletionCommandHandler(
            IServiceProvider serviceProvider,
            ICompletionBroker completionBroker,
            ITextView textView,
            IVsTextView textViewAdapter,
            CompletionEngine completionEngine)
        {
            _serviceProvider = serviceProvider;
            _completionBroker = completionBroker;
            _textView = textView;
            _engine = completionEngine;

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
                if (HandleSessionCompletion(c))
                {
                    return VSConstants.S_OK;
                }

                if (_session == null && (c == '\'' || c == '"'))
                {
                    // If a completion session isn't active, and we type a quote, check
                    // if a quote already exists at the position & just move the cursor
                    // so we don't get a double quote
                    // If a completion session is active, that's handled there
                    var cursorPos = _textView.Caret.Position.BufferPosition;
                    var nextChar = _textView.TextSnapshot.GetText(cursorPos, 1)[0];
                    if (nextChar == c)
                    {
                        _textView.Caret.MoveTo(cursorPos + 1);
                        return VSConstants.S_OK;
                    }
                }
                var result = _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                if (HandleSessionStart(c))
                {
                    return VSConstants.S_OK;
                }

                if (HandleSessionUpdate())
                {
                    return VSConstants.S_OK;
                }

                return result;
            }

            return _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private bool HandleSessionStart(char c)
        {
            // If the pressed key is a key that can start a completion session.
            if (CompletionEngine.ShouldTriggerCompletionListOn(c) || c == '\a')
            {
                var session = _session;
                if (session == null || session.IsDismissed)
                {
                    if (TriggerCompletion() && c != '<' && c != '.' && c != ' ' && c != '[' && c != '(' && c != '|' && c != '#' && c != '/')
                    {
                        session?.Filter();
                    }

                    return true;
                }
            }
            else if (c == ',')
            {
                var session = _session;
                if (session is null || session.IsDismissed)
                {
                    if (TriggerCompletion())
                    {
                        session?.Filter();
                    }
                    return true;
                }
            }
            return false;
        }

        private bool HandleSessionUpdate()
        {
            if (_session is { } session && !session.IsDismissed)
            {
                session.Filter();
                return true;
            }
            return false;
        }

        private bool HandleSessionCompletion(char c)
        {
            var line = _textView.GetTextViewLineContainingBufferPosition(
                _textView.Caret.Position.BufferPosition);
            var start = line.Start;
            var end = Math.Min(line.End, _textView.Caret.Position.BufferPosition);

            // Adding a xmlns is special-cased here because we don't want '.' triggering
            // a completion, which can complete on the wrong value
            // So we only trigger on ' ' or '\t', and swallow that so it doesn't get 
            // inserted into the text buffer
            var session = _session;
            if (session is not null && !session.IsDismissed)
            {
                var text = line.Snapshot.GetText(start, end - start);

                if (text.Contains("xmlns"))
                {
                    if (char.IsWhiteSpace(c))
                    {
                        session.Commit();
                        return true;
                    }
                    else if (c == ':')
                    {
                        session.Dismiss();
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

            if (char.IsWhiteSpace(c)
                || c == '\'' || c == '"' || c == '=' || c == '>' || c == '.'
                || c == '#' || c == ')' || c == ']')
            {
                if (session != null && !session.IsDismissed &&
                    session.SelectedCompletionSet.SelectionStatus.IsSelected)
                {
                    var selected = session.SelectedCompletionSet.SelectionStatus.Completion as XamlCompletion;

                    var bufferPos = _textView.Caret.Position.BufferPosition;

                    session.Commit();

                    if (selected.DeleteTextOffset is int rof)
                    {
                        var newCursorPos = bufferPos.Add(rof);
                        SnapshotSpan deleteSpan = newCursorPos < bufferPos
                            ? new(newCursorPos, -rof)
                            : new(bufferPos, rof);
                        _textView.TextBuffer.Delete(deleteSpan);
                    }

                    if (selected?.CursorOffset > 0)
                    {
                        // Offset the cursor if necessary e.g. to place it within the quotation
                        // marks of an attribute.
                        var cursorPos = _textView.Caret.Position.BufferPosition;
                        var newCursorPos = cursorPos - selected.CursorOffset;
                        _textView.Caret.MoveTo(newCursorPos);
                    }

                    // special-cased avoid TriggerCompletion
                    if (selected.InsertionText == "xmlns:")
                    {
                        return true;
                    }

                    // Ideally, we should only parse the current line of text, where the parser State would return
                    // 'None' if you're spreading control attributes out across multiple lines
                    // BUT, Selectors can span multiple lines (aggregates separated by ',') and this theory
                    // breaks down & and there's no way to determine XML context from just the current line
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
                        var type = _engine.Helper.LookupType(parser.TagName);
                        if (type != null && type.Events.FirstOrDefault(x => x.Name == parser.AttributeName) != null)
                        {
                            GenerateEventHandlerAsync(type.FullName, parser.AttributeName, selected.InsertionText)
                                .FireAndForget();
                        }
                        var isSelector = parser.AttributeName?.Equals("Selector") == true;
                        if (char.IsWhiteSpace(c))
                        {
                            // For most xml attributes, swallow the space upon completion
                            // For selector, allow it to go into the buffer
                            // Also if in a markupextention
                            skip = !(isSelector && c != '\n' && c != '\t');

                            // If we're in a markup extension, only swallow the space if the
                            // completion isn't on the Markup extension
                            // i.e., where | is the cursor
                            // {DynamicResource -> {DynamicResource |
                            // but {Binding Path= -> {Binding Path=|
                            // similarly, more embedded things like RelativeSource work the same way
                            // {Binding path, RelativeSource={RelativeSource -> ...={RelativeSource |
                            if (parser.AttributeValue?.StartsWith("{") == true)
                            {
                                // If press Tab or CR in expression ignore it in completation session
                                if (c is '\t' or '\n')
                                {
                                    return true;
                                }
                                // To determine, we'll walk back the text from the cursor position
                                // until we hit either something that isn't a character
                                // If that's a {, we apply the space, otherwise we dont
                                // Only using the line text (up to cursor) since xaml can't wrap
                                // Also ignore ':' for namespaces or directives
                                var text = line.Snapshot.GetText(start, end - start);
                                for (int i = text.Length - 1; i >= 0; i--)
                                {
                                    var lineChar = text[i];
                                    if (char.IsLetterOrDigit(lineChar) || lineChar == ':')
                                        continue;

                                    // any other character than [A-z,0-9,:] is a different part
                                    skip = lineChar != '{';
                                    break;
                                }

                                // if in a markup extension, if we skip the entered char, we won't get
                                // to start a new completion session, so force start it
                                // The check for '=' in the insertion text ensures we don't always get this
                                // e.g., {OnPlatform Wind -> {OnPlatform Windows= [New completion session]
                                // but {OnPlatform Windows=Re -> {OnPlatform Windows=Red [no new session]
                                if (skip && selected.InsertionText.EndsWith("="))
                                    TriggerCompletion();
                            }
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

                        var lastInsertionChar = (selected.InsertionText?.Length ?? 0) > 0
                            ? selected.InsertionText[selected.InsertionText.Length - 1]
                            : default;

                        // Cases like {Binding Path= result in {Binding Path==
                        // as the completion includes the '=', if the entered char
                        // is the same as the last char here, swallow the entered char
                        if (!skip && lastInsertionChar == c)
                        {
                            skip = true;

                            // Specifically for markup extensions, make sure '=' triggers
                            // a new completion session when entered, but only if we're
                            // skipping the char entered
                            if (c == '=')
                                TriggerCompletion();
                        }
                        else if (isSelector && lastInsertionChar is '=' or '.')
                        {
                            // Trigger Selector property Value Completation
                            if (c is not '=' or '.')
                                TriggerCompletion();
                        }
                    }
                    else if (state != XmlParser.ParserState.StartElement || selected.TriggerCompletion)
                    {
                        TriggerCompletion();
                    }

                    return skip;
                }
                else
                {
                    session?.Dismiss();
                    return false;
                }
            }
            else if (c == ':' && (session != null && !session.IsDismissed))
            {
                var parser = XmlParser.Parse(_textView.TextSnapshot.GetText().AsMemory(), 0, end);
                var state = parser.State;

                if (state == XmlParser.ParserState.AttributeValue &&
                    parser.AttributeName?.Equals("Selector") == true)
                {
                    // Force new session to start to suggest pseudoclasses
                    session.Dismiss();
                    return false;
                }
            }
            else if (c == '(' && session?.IsDismissed == false)
            {
                var parser = XmlParser.Parse(_textView.TextSnapshot.GetText().AsMemory(), 0, end);
                var state = parser.State;
                if ((state == XmlParser.ParserState.AttributeValue || state == XmlParser.ParserState.AfterAttributeValue)
                    && parser.AttributeName?.Equals("Selector") == true)
                {
                    session.Dismiss();
                    return false;
                }
            }
            else if (c == '{' && (session != null && !session.IsDismissed))
            {
                var parser = XmlParser.Parse(_textView.TextSnapshot.GetText().AsMemory(), 0, end);
                var state = parser.State;

                if (state == XmlParser.ParserState.AttributeValue)
                {
                    // For something like Brushes, restart the completion session if we want
                    // a markup extension
                    session.Dismiss();
                    return false;
                }
            }
            else if (c == ',' && (session != null && !session.IsDismissed))
            {
                // Typing the comma in a markup extension should trigger a new completion session
                var text = line.Snapshot.GetText(start, end - start);
                for (int i = text.Length - 1; i >= 0; i--)
                {
                    if (text[i] == '{')
                    {
                        session.Dismiss();
                        return false;
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

            var session = existingSession ?? _completionBroker.CreateCompletionSession(
                _textView,
                caretPoint?.Snapshot.CreateTrackingPoint(caretPoint.Value.Position, PointTrackingMode.Positive),
                true);

            // Subscribe to the Dismissed event on the session.
            session.Dismissed += SessionDismissed;
            _session = session;
            session.Start();
            return true;
        }

        private void SessionDismissed(object sender, EventArgs e)
        {
            var session = _session;
            _session = null;
            session.Dismissed -= SessionDismissed;
        }

        private IEnumerable<ParameterSyntax> GetParametersList(string[] parameterTypes, string[] parameterNames)
        {
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                yield return SyntaxFactory.Parameter(SyntaxFactory.List<AttributeListSyntax>(),
                                                     SyntaxFactory.TokenList(),
                                                     SyntaxFactory.ParseTypeName(parameterTypes[i]),
                                                     SyntaxFactory.Identifier(parameterNames[i]),
                                                     null);
            }
        }

        private async System.Threading.Tasks.Task GenerateEventHandlerAsync(string controlType, string eventName, string generatedMethodName)
        {
            var currentScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            try
            {
                var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
                var workspace = componentModel.GetService<VisualStudioWorkspace>();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var activeDocument = dte.ActiveDocument;
                var activeDocumentName = $"{activeDocument.Name}.cs";
                if (activeDocument.ProjectItem?.ContainingProject?.UniqueName is { } uniqueName)
                {
                    var currentDocumentCodeBehind = workspace.CurrentSolution.Projects
                         .FirstOrDefault(x => x.FilePath?.EndsWith(uniqueName) == true)
                         .Documents
                         .FirstOrDefault(x => string.Equals(x.Name, activeDocumentName, StringComparison.OrdinalIgnoreCase));

                    if (currentDocumentCodeBehind is null)
                    {
                        return;
                    }

                    var compilation = await currentDocumentCodeBehind.Project.GetCompilationAsync();
                    var root = await currentDocumentCodeBehind.GetSyntaxRootAsync();
                    var codeBehindClass = root.DescendantNodes()
                        .FirstOrDefault(x => x.IsKind(SyntaxKind.ClassDeclaration)) as ClassDeclarationSyntax;

                    await TaskScheduler.Default;

                    var currentEvent = GetAllEvents(compilation.References.Select(compilation.GetAssemblyOrModuleSymbol)
                        .OfType<IAssemblySymbol>().Select(a => a.GetTypeByMetadataName(controlType))
                        .FirstOrDefault(x => x != null))
                        .FirstOrDefault(x => x.Name == eventName) as IEventSymbol;
                    var parameters = (currentEvent.Type as INamedTypeSymbol).DelegateInvokeMethod.Parameters;
                    string[] parameterNames = new string[parameters.Length];
                    string[] parameterTypes = new string[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        parameterNames[i] = parameters[i].MetadataName;
                        parameterTypes[i] = parameters[i].Type.ToString();
                    }
                    var methodToInsert = GetMethodDeclarationSyntax("void", generatedMethodName, parameterTypes, parameterNames);
                    var duplicatingMethodIds = new List<int>();
                    foreach (MethodDeclarationSyntax item in codeBehindClass.DescendantNodes().Where(x => x.IsKind(SyntaxKind.MethodDeclaration)))
                    {
                        if (item.ReturnType is PredefinedTypeSyntax predefinedTypeSyntax &&
                            predefinedTypeSyntax.Keyword.IsKind(SyntaxKind.VoidKeyword))
                        {
                            var itemParameters = item.ParameterList.Parameters.Select(x => x.Type.ToString()).ToArray();
                            var methodToInsertParameters = methodToInsert.ParameterList.Parameters.Select(x => x.Type.ToString()).ToArray();
                            if (itemParameters.Length == methodToInsertParameters.Length)
                            {
                                var sameMethods = true;
                                for (int i = 0; i < itemParameters.Length; i++)
                                {
                                    if (itemParameters[i] != methodToInsertParameters[i])
                                    {
                                        sameMethods = false;
                                        break;
                                    }
                                }

                                if (sameMethods)
                                {
                                    var methodNameParts = item.Identifier.Text.Split('_');
                                    if (methodNameParts.Length == 3 && int.TryParse(methodNameParts.Last(), out var methodId))
                                    {
                                        duplicatingMethodIds.Add(methodId);
                                    }
                                    else
                                    {
                                        duplicatingMethodIds.Add(0);
                                    }
                                }
                            }
                        }
                    }

                    if (duplicatingMethodIds.Count > 0)
                    {
                        methodToInsert = methodToInsert.WithIdentifier(SyntaxFactory.Identifier(generatedMethodName + $"_{duplicatingMethodIds.Max() + 1}"));
                    }
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var newMethodDeclaration = codeBehindClass.AddMembers(methodToInsert);
                    var newRoot = root.ReplaceNode(codeBehindClass, newMethodDeclaration);
                    newRoot = Formatter.Format(newRoot, Formatter.Annotation, workspace);
                    workspace.TryApplyChanges(currentDocumentCodeBehind.WithSyntaxRoot(newRoot).Project.Solution);

                    // Hack to add method id to xaml file because i can't find a way to generate it from completions
                    // Apply these changes after adding method because otherwise workspace will fail to add method
                    if (duplicatingMethodIds.Count > 0)
                    {
                        var textDocument = dte.ActiveDocument.Object() as EnvDTE.TextDocument;
                        var editPoint = textDocument.CreateEditPoint();
                        editPoint.MoveToAbsoluteOffset(textDocument.Selection.ActivePoint.AbsoluteCharOffset);
                        editPoint.Insert($"_{duplicatingMethodIds.Max() + 1}");
                    }
                }
            }
            finally
            {
                if (currentScheduler is not null && currentScheduler.Id != TaskScheduler.FromCurrentSynchronizationContext()?.Id)
                {
                    await currentScheduler;
                }
            }
        }

        private MethodDeclarationSyntax GetMethodDeclarationSyntax(string returnTypeName, string methodName, string[] parameterTypes, string[] paramterNames)
        {
            var parameterList = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(GetParametersList(parameterTypes, paramterNames)));
            return SyntaxFactory.MethodDeclaration(SyntaxFactory.List<AttributeListSyntax>(),
                                                   SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)),
                                                   SyntaxFactory.ParseTypeName(returnTypeName),
                                                   null,
                                                   SyntaxFactory.Identifier(methodName),
                                                   null,
                                                   parameterList,
                                                   SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(),
                                                   SyntaxFactory.Block(),
                                                   SyntaxFactory.Token(SyntaxKind.None)).WithAdditionalAnnotations(Formatter.Annotation);
        }

        private static IEnumerable<ISymbol> GetAllEvents(INamedTypeSymbol t)
        {
            foreach (var p in t.GetMembers().Where(x => x.Kind == SymbolKind.Event))
                yield return p;
            if (t.BaseType != null)
                foreach (var p in GetAllEvents(t.BaseType))
                    yield return p;
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
