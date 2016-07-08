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
using AvaloniaVS.Infrastructure;
using Sandbox;

namespace AvaloniaVS.IntelliSense
{
    class CompletionSource : ICompletionSource
    {
        private static readonly HashSet<string> _staticGetters = new HashSet<string>
        {
            "StaticExtension",
            "TypeExtension",
            "TypeExtension.TypeName=",
            "TypeExtension.Type=",
            "BindingExtension.Converter=",
            "BindingExtension.Source=",
            "BindingExtension.ConverterParameter=",
            "BindingExtension.ConverterParameter=",
            "BindingExtension.FallbackValue=",
        };

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

            public IEnumerable<string> FilterStaticGettersTypeNames(string prefix)
                    => FilterTypeNames(prefix, false, false, true);

            public IEnumerable<string> FilterTypeNames(string prefix)
                    => FilterTypeNames(prefix, false, false, false);

            public IEnumerable<string> FilterTypeNames(string prefix, bool withAttachedPropertiesOnly)
                    => FilterTypeNames(prefix, withAttachedPropertiesOnly, false, false);

            public IEnumerable<string> FilterMarkupExtensionsTypeNames(string prefix)
                    => FilterTypeNames(prefix, false, true, false);

            public IEnumerable<string> FilterTypeNames(string prefix,
                                bool withAttachedPropertiesOnly,
                                bool markupExtensionsOnly,
                                bool withStaticGettersOnly)
            {
                prefix = prefix ?? "";
                var e = _types
                    .Where(t => t.Key.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase));
                if (withAttachedPropertiesOnly)
                    e = e.Where(t => t.Value.HasAttachedProperties);
                if (markupExtensionsOnly)
                    e = e.Where(t => t.Value.IsMarkupExtension);
                if (withStaticGettersOnly)
                    e = e.Where(t => t.Value.HasStaticGetters);
                return e.Select(s => s.Key);
            }

            public MetadataType LookupType(string name)
            {
                MetadataType rv;
                _types.TryGetValue(name, out rv);
                return rv;
            }

            public IEnumerable<string> FilterPropertyNames(string typeName, string propName)
                => FilterPropertyNames(typeName, propName, false, false);

            public IEnumerable<string> FilterPropertyNames(string typeName, string propName, bool attachedOnly)
                => FilterPropertyNames(typeName, propName, false, attachedOnly);

            public IEnumerable<string> FilterStaticGetterPropertyNames(string typeName, string propName)
                => FilterPropertyNames(typeName, propName, true, false);

            public IEnumerable<string> FilterPropertyNames(string typeName, string propName, bool staticGetterOnly, bool attachedOnly)
            {
                var t = LookupType(typeName);
                propName = propName ?? "";
                if (t == null)
                    return new string[0];
                var e = t.Properties.Where(p => p.Name.StartsWith(propName, StringComparison.InvariantCultureIgnoreCase));

                if (staticGetterOnly)
                    e = e.Where(p => p.IsStatic && p.HasGetter);

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
                rv[""] = Utils.AvaloniaNamespace;
            return rv;
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            Metadata metadata;
            _textBuffer.Properties.TryGetProperty(typeof(Metadata), out metadata);
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
                    var split = state.AttributeName.Split(new[] { '.' }, 2);
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
                    ((CompletionCommandHandler)session.TextView.Properties[typeof(CompletionCommandHandler)])
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

                //Markup extension, ignore everything else
                if (state.AttributeValue.StartsWith("{"))
                {
                    curStart = state.CurrentValueStart.Value +
                               BuildCompletionsForMarkupExtension(completions,
                                   text.Substring(state.CurrentValueStart.Value));
                }
                else
                {
                    if (prop?.Type == MetadataPropertyType.Enum && prop.EnumValues != null)
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
            }

            if (completions.Count != 0)
            {
                var span = new SnapshotSpan(pos.Snapshot, curStart, pos.Position - curStart);
                completionSets.Insert(0, new CompletionSet("Avalonia", "Avalonia",
                    pos.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive), completions, null));
            }
        }



        int BuildCompletionsForMarkupExtension(List<Completion> completions, string data)
        {
            int? forcedStart = null;
            var ext = MarkupExtensionParser.Parse(data);

            var transformedName = (ext.ElementName ?? "").Trim();
            if (_helper.LookupType(transformedName)?.IsMarkupExtension != true)
                transformedName += "Extension";

            if (ext.State == MarkupExtensionParser.ParserStateType.StartElement)
                completions.AddRange(_helper.FilterMarkupExtensionsTypeNames(ext.ElementName)
                    .Select(t => t.EndsWith("Extension") ? t.Substring(0, t.Length - "Extension".Length) : t)
                    .Select(t => new Completion(t)));
            if (ext.State == MarkupExtensionParser.ParserStateType.StartAttribute ||
                ext.State == MarkupExtensionParser.ParserStateType.InsideElement)
            {
                if (ext.State == MarkupExtensionParser.ParserStateType.InsideElement)
                    forcedStart = data.Length;

                completions.AddRange(_helper.FilterPropertyNames(transformedName, ext.AttributeName ?? "")
                        .Select(x => new Completion(x, x + "=", x, null, null)));

                if (HasStaticGetterCompletition(transformedName))
                {
                    BuildCompletionsStaticGetters(completions, ext.AttributeName);
                }
            }
            if (ext.State == MarkupExtensionParser.ParserStateType.AttributeValue)
            {
                var prop = _helper.LookupProperty(transformedName, ext.AttributeName);

                if (prop?.Type == MetadataPropertyType.Enum && prop.EnumValues != null)
                    completions.AddRange(prop.EnumValues.Select(v => new Completion(v)));
            }

            if (ext.State == MarkupExtensionParser.ParserStateType.BeforeAttributeValue ||
                ext.State == MarkupExtensionParser.ParserStateType.AttributeValue)
            {
                if (HasStaticGetterCompletition(transformedName, ext.AttributeName))
                {
                    BuildCompletionsStaticGetters(completions, ext.AttributeValue);
                }
            }

            return forcedStart ?? ext.CurrentValueStart;
        }



        private bool HasStaticGetterCompletition(string markup, string markupProperty = null)
        {
            string key = markupProperty == null ? markup : $"{markup}.{markupProperty}";

            return _staticGetters.Contains(key);
        }

        private void BuildCompletionsStaticGetters(List<Completion> completions, string currentValue)
        {
            if (!string.IsNullOrEmpty(currentValue) && currentValue.Contains("."))
            {
                var typeAndProp = currentValue.Split(new[] { '.' }, 2);

                completions.AddRange(_helper.FilterStaticGetterPropertyNames(typeAndProp[0], typeAndProp[1])
                .Select(x => new Completion(x, $"{typeAndProp[0]}.{x}", x, null, null)));
            }
            else
            {
                completions.AddRange(_helper.FilterStaticGettersTypeNames(currentValue)
                   .Select(x => new Completion(x, x + ".", x, null, null)));
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
