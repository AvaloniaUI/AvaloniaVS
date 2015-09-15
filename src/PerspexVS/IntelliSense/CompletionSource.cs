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

        class MetadataHelper
        {
            //TODO: detect root namespace
            //TODO: add support for user's namespaces

            private List<string> _rootNamespaces;
            private Dictionary<string, MetadataType> _rootNsTypes;
            private PerspexDesignerMetadata _oldMetadata;
            public void SetMetadata(PerspexDesignerMetadata metadata)
            {
                if (_oldMetadata == metadata)
                    return;
                _oldMetadata = metadata;

                
                var rootNs = "https://github.com/grokys/Perspex";
                _rootNamespaces =
                    metadata.NamespaceAliases.Where(n => n.XmlNamespace == rootNs).Select(x => x.Namespace).ToList();
                _rootNsTypes = metadata.Types.Where(t => _rootNamespaces.Contains(t.Namespace)).ToDictionary(t => t.Name);
            }

            public IEnumerable<string> FilterTypeNames(string prefix) =>
                _rootNsTypes.Values
                    .Where(t => t.Name.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
                    .Select(s => s.Name);

            public MetadataType LookupType(string name)
            {
                MetadataType rv;
                _rootNsTypes.TryGetValue(name, out rv);
                return rv;
            }

            public IEnumerable<string> FilterPropertyNames(string typeName, string propName)
            {
                var t = LookupType(typeName);
                propName = propName ?? "";
                return
                    t?.Properties.Where(p => p.Name.StartsWith(propName, StringComparison.InvariantCultureIgnoreCase))
                        .Select(p => p.Name) ?? new string[0];
            }

            public MetadataProperty LookupProperty(string typeName, string propName) 
                => LookupType(typeName)?.Properties?.FirstOrDefault(p => p.Name == propName);
        }

        MetadataHelper _helper = new MetadataHelper();

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
            _helper.SetMetadata(metadata);



            var curStart = state.CurrentValueStart ?? 0;

            if (state.State == XmlParser.ParserState.StartElement)
            {
                var tagName = state.TagName;
                if (tagName.Contains("."))
                {
                    var dotPos = tagName.IndexOf(".");
                    var typeName = tagName.Substring(0, dotPos);
                    var compName = tagName.Substring(dotPos + 1, 0);
                    curStart = curStart + dotPos + 1;
                    completions.AddRange(_helper.FilterPropertyNames(typeName, compName).Select(p => new Completion(p)));
                }
                else
                    completions.AddRange(_helper.FilterTypeNames(tagName).Select(x => new Completion(x)));
            }
            else if (state.State == XmlParser.ParserState.InsideElement ||
                     state.State == XmlParser.ParserState.StartAttribute)
            {

                if (state.State == XmlParser.ParserState.InsideElement)
                    curStart = pos.Position; //Force completion to be started from current cursor position

                completions.AddRange(_helper.FilterPropertyNames(state.TagName, state.AttributeName)
                    .Select(x => new Completion(x, x + "=\"\"", x, null, null)));

                session.Committed += delegate
                {
                    session.TextView.Caret.MoveToPreviousCaretPosition();
                    //Automagically trigger new completion for attribute enums
                    ((CompletionCommandHandler) session.TextView.Properties[typeof (CompletionCommandHandler)])
                        .TriggerNew();
                };
            }
            else if (state.State == XmlParser.ParserState.AttributeValue)
            {
                var prop = _helper.LookupProperty(state.TagName, state.AttributeName);
                if (prop?.Type == MetadataPropertyType.Enum)
                    completions.AddRange(prop.EnumValues.Select(v => new Completion(v)));
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
