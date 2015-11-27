using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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
            private Metadata _metadata;
            public Dictionary<string, string> Aliases { get; private set; }

            Dictionary<string, MetadataType> _types;

            public void SetMetadata(Metadata metadata, string xml)
            {
                var aliases = GetNamespaceAliases(xml);
                
                //Check if metadata and aliases can be reused
                if (_metadata == metadata && Aliases != null && _types != null)
                {
                    if (aliases.Count == Aliases.Count)
                    {
                        bool mismatch = false;
                        foreach (var alias in aliases)
                        {
                            if (!Aliases.ContainsKey(alias.Key) || Aliases[alias.Key] != alias.Value)
                            {
                                mismatch = true;
                                break;
                            }
                        }

                        if (!mismatch)
                            return;
                    }
                }
                Aliases = aliases;
                _metadata = metadata;
                _types = null;
                var types = new Dictionary<string, MetadataType>();
                foreach (var alias in Aliases)
                {
                    Dictionary<string, MetadataType> ns;
                    if (!metadata.Namespaces.TryGetValue(alias.Value, out ns))
                        continue;
                    var prefix = alias.Key.Length == 0 ? "" : (alias.Key + ":");
                    foreach (var type in ns.Values)
                        types[prefix + type.Name] = type;
                }
                _types = types;

            }


            public IEnumerable<string> FilterTypeNames(string prefix, bool withAttachedPropertiesOnly = false)
            {
                prefix = prefix ?? "";
                var e = _types
                    .Where(t => t.Key.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase));
                if (withAttachedPropertiesOnly)
                    e = e.Where(t => t.Value.HasAttachedProperties);
                return e.Select(s => s.Key);
            }

            MetadataType LookupType(string name)
            {
                MetadataType rv;
                _types.TryGetValue(name, out rv);
                return rv;
            }

            public IEnumerable<string> FilterPropertyNames(string typeName, string propName, bool attachedOnly = false)
            {
                var t = LookupType(typeName);
                propName = propName ?? "";
                var e = t.Properties.Where(p => p.Name.StartsWith(propName, StringComparison.InvariantCultureIgnoreCase));
                if (attachedOnly)
                    e = e.Where(p => p.IsAttached);
                return e.Select(p => p.Name);
            }

            public MetadataProperty LookupProperty(string typeName, string propName) 
                => LookupType(typeName)?.Properties?.FirstOrDefault(p => p.Name == propName);
        }

        MetadataHelper _helper = new MetadataHelper();

        static Dictionary<string, string> GetNamespaceAliases(string xml)
        {
            var rv = new Dictionary<string, string>();
            try
            {
                var xmlRdr = XmlReader.Create(new StringReader(xml));
                while (xmlRdr.NodeType != XmlNodeType.Element)
                {
                    xmlRdr.Read();
                }

                for (var c = 0; c < xmlRdr.AttributeCount; c++)
                {
                    xmlRdr.MoveToAttribute(c);
                    var ns = xmlRdr.Name;
                    if (ns != "xmlns" && !ns.StartsWith("xmlns:"))
                        continue;
                    ns = ns == "xmlns" ? "" : ns.Substring(6);
                    rv[ns] = xmlRdr.Value;
                }

                
            }
            catch 
            {
                //
            }
            if (!rv.ContainsKey(""))
                    rv[""] = Utils.PerspexNamespace;
            return rv;
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            Metadata metadata;
            _textBuffer.Properties.TryGetProperty(typeof (Metadata), out metadata);
            if (metadata == null)
                return;

            var pos = session.TextView.Caret.Position.BufferPosition;
            var text = pos.Snapshot.GetText(0, pos.Position);
            var state = XmlParser.Parse(text);
            

            var completions = new List<Completion>();
            _helper.SetMetadata(metadata, _textBuffer.CurrentSnapshot.GetText());
            
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

                if (state.AttributeName?.Contains(".") == true)
                {
                    var dotPos = state.AttributeName.IndexOf('.');
                    curStart += dotPos + 1;
                    var split = state.AttributeName.Split(new[] {'.'}, 2);
                    completions.AddRange(_helper.FilterPropertyNames(split[0], split[1], true)
                        .Select(x => new Completion(x, x + "=\"\"", x, null, null)));
                }
                else
                {
                    completions.AddRange(_helper.FilterPropertyNames(state.TagName, state.AttributeName).Select(x => new Completion(x, x + "=\"\"", x, null, null)));
                    completions.AddRange(
                        _helper.FilterTypeNames(state.AttributeName, true)
                            .Select(v => new Completion(v, v + ". ", v, null, null)));
                }


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
                MetadataProperty prop;
                if (state.AttributeName.Contains("."))
                {
                    //Attached property
                    var split = state.AttributeName.Split('.');
                    prop = _helper.LookupProperty(split[0], split[1]);
                }
                else
                    prop = _helper.LookupProperty(state.TagName, state.AttributeName);


                if (prop?.Type == MetadataPropertyType.Enum)
                    completions.AddRange(prop.EnumValues.Select(v => new Completion(v)));
                else if (state.AttributeName == "xmlns" || state.AttributeName.Contains("xmlns:"))
                {
                    if (state.AttributeValue.StartsWith("clr-namespace:"))
                        completions.AddRange(
                            metadata.Namespaces.Keys.Where(v => v.StartsWith(state.AttributeValue))
                                .Select(v => new Completion(v)));
                    else
                    {
                        completions.Add(new Completion("clr-namespace:"));
                        completions.AddRange(
                            metadata.Namespaces.Keys.Where(
                                v =>
                                    v.StartsWith(state.AttributeValue) &&
                                    !"clr-namespace".StartsWith(state.AttributeValue))
                                .Select(v => new Completion(v)));
                    }
                }
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
