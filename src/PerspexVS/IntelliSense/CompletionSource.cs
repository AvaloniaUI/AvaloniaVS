using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Perspex.Designer;
using Perspex.Designer.Metadata;
using Sandbox;

namespace PerspexVS.IntelliSense
{
    class CompletionSource : ICompletionSource
    {
        private readonly ITextBuffer _textBuffer;
        private readonly CompletionSourceProvider _provider;

        public CompletionSource(ITextBuffer textBuffer, CompletionSourceProvider provider)
        {
            _textBuffer = textBuffer;
            _provider = provider;
        }

        public void Dispose()
        {
        }

        private ITrackingSpan FindTokenSpanAtPosition(ITrackingPoint point, ICompletionSession session)
        {
            SnapshotPoint currentPoint = (session.TextView.Caret.Position.BufferPosition);
            ITextStructureNavigator navigator = _provider.NavigatorService.GetTextStructureNavigator(_textBuffer);
            TextExtent extent = navigator.GetExtentOfWord(currentPoint);
            return currentPoint.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            PerspexDesigner designer;
            _textBuffer.Properties.TryGetProperty(typeof (PerspexDesigner), out designer);
            var metadata = designer?.Metadata;
            if (metadata == null)
                return;

            var pos = session.TextView.Caret.Position.BufferPosition;
            var text = pos.Snapshot.GetText(0, pos.Position);
            var state = XmlParser.Parse(text);

            var completions = new List<Completion>();

            //TODO: detect root namespace
            var rootNs = "https://github.com/grokys/Perspex";
            var rootNamespaces =
                metadata.NamespaceAliases.Where(n => n.XmlNamespace == rootNs).Select(x => x.Namespace).ToList();
            var rootNsTypes = metadata.Types.Where(t => rootNamespaces.Contains(t.Namespace)).ToDictionary(t => t.Name);


            var curStart = state.CurrentValueStart ?? 0;

            if (state.State == XmlParser.ParserState.StartElement)
            {
                var tagName = state.TagName;
                completions.AddRange(rootNsTypes.Values
                    .Where(t => t.Name.StartsWith(tagName, StringComparison.InvariantCultureIgnoreCase))
                    .Select(metadataType => new Completion(metadataType.Name)));
            }
            else if(state.State == XmlParser.ParserState.InsideElement || state.State == XmlParser.ParserState.StartAttribute)
            {
                if (state.State == XmlParser.ParserState.InsideElement)
                    curStart = pos.Position;
                MetadataType type;
                if (rootNsTypes.TryGetValue(state.TagName, out type))
                {
                    var attrName = state.AttributeName ?? "";
                    completions.AddRange(type.Properties
                        .Where(p => p.Name.StartsWith(attrName, StringComparison.InvariantCultureIgnoreCase))
                        .Select(prop => new Completion(prop.Name, prop.Name + "=\"\"", prop.Name, null, null)));
                }
                session.Committed += delegate
                {
                    session.TextView.Caret.MoveToPreviousCaretPosition();
                };
            }

            if (completions.Count != 0)
            {
                var span = new SnapshotSpan(pos.Snapshot, curStart, pos.Position - curStart);
                completionSets.Insert(0, new CompletionSet("Perspex", "Perspex",
                    pos.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive), completions, null));
            }
        }
    }

    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("xml")]
    [Name("token completion")]
    internal class CompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new CompletionSource(textBuffer, this);
        }
    }
}
