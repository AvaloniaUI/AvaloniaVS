using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Ide.CompletionEngine;
using System.Text.RegularExpressions;
using System.Xml;
using AvMetadata = Avalonia.Ide.CompletionEngine.Metadata;
using AvaloniaVS.Shared.Completion.Metadata;
using AvCompletion = Avalonia.Ide.CompletionEngine.Completion;

namespace AvaloniaVS.Shared.Completion
{
    public class AvVSCompletionEngine
    {
        private class MetadataHelper
        {
            private AvMetadata _metadata;
            public AvMetadata Metadata => _metadata;
            public Dictionary<string, string> Aliases { get; private set; }

            private Dictionary<string, MetadataType> _types;
            private string _currentAssemblyName;

            public void SetMetadata(AvMetadata metadata, string xml, string currentAssemblyName = null)
            {
                var aliases = GetNamespaceAliases(xml);

                //Check if metadata and aliases can be reused
                if (_metadata == metadata && Aliases != null && _types != null && currentAssemblyName == _currentAssemblyName)
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
                _currentAssemblyName = currentAssemblyName;

                var types = new Dictionary<string, MetadataType>();
                foreach (var alias in Aliases.Concat(new[] { new KeyValuePair<string, string>("", "") }))
                {
                    Dictionary<string, MetadataType> ns;

                    string aliasValue = alias.Value ?? "";

                    if (!string.IsNullOrEmpty(_currentAssemblyName) && aliasValue.StartsWith("clr-namespace:") && !aliasValue.Contains(";assembly="))
                        aliasValue = $"{aliasValue};assembly={_currentAssemblyName}";

                    if (!metadata.Namespaces.TryGetValue(aliasValue, out ns))
                        continue;

                    var prefix = alias.Key.Length == 0 ? "" : (alias.Key + ":");
                    foreach (var type in ns.Values)
                        types[prefix + type.Name] = type;
                }

                _types = types;
            }

            public IEnumerable<KeyValuePair<string, MetadataType>> FilterTypes(string prefix, bool withAttachedPropertiesOrEventsOnly = false, bool markupExtensionsOnly = false, bool staticGettersOnly = false, bool xamlDirectiveOnly = false)
            {
                prefix = prefix ?? "";

                var e = _types
                    .Where(t => t.Value.IsXamlDirective == xamlDirectiveOnly && t.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (withAttachedPropertiesOrEventsOnly)
                    e = e.Where(t => t.Value.HasAttachedProperties || t.Value.HasAttachedEvents);
                if (markupExtensionsOnly)
                    e = e.Where(t => t.Value.IsMarkupExtension);
                if (staticGettersOnly)
                    e = e.Where(t => t.Value.HasStaticGetProperties);

                return e;
            }

            public IEnumerable<string> FilterTypeNames(string prefix, bool withAttachedPropertiesOrEventsOnly = false, bool markupExtensionsOnly = false, bool staticGettersOnly = false, bool xamlDirectiveOnly = false)
            {
                return FilterTypes(prefix, withAttachedPropertiesOrEventsOnly, markupExtensionsOnly, staticGettersOnly, xamlDirectiveOnly).Select(s => s.Key);
            }

            public MetadataType LookupType(string name)
            {
                MetadataType rv;
                _types.TryGetValue(name, out rv);
                return rv;
            }

            public IEnumerable<string> FilterPropertyNames(string typeName, string propName,
                bool? attached,
                bool hasSetter,
                bool staticGetter = false)
            {
                var t = LookupType(typeName);
                return FilterPropertyNames(t, propName, attached, hasSetter, staticGetter);
            }

            public IEnumerable<string> FilterPropertyNames(MetadataType t, string propName,
                bool? attached,
                bool hasSetter,
                bool staticGetter = false)
            {

                propName = propName ?? "";
                if (t == null)
                    return new string[0];

                var e = t.Properties.Where(p => p.Name.StartsWith(propName, StringComparison.OrdinalIgnoreCase) && (hasSetter ? p.HasSetter : p.HasGetter));

                if (attached.HasValue)
                    e = e.Where(p => p.IsAttached == attached);
                if (staticGetter)
                    e = e.Where(p => p.IsStatic && p.HasGetter);
                else
                    e = e.Where(p => !p.IsStatic);

                return e.Select(p => p.Name);
            }

            public IEnumerable<string> FilterEventNames(string typeName, string propName,
                bool attached)
            {
                var t = LookupType(typeName);
                propName = propName ?? "";
                if (t == null)
                    return new string[0];

                return t.Events.Where(n => n.IsAttached == attached && n.Name.StartsWith(propName)).Select(n => n.Name);
            }

            public MetadataProperty LookupProperty(string typeName, string propName)
                => LookupType(typeName)?.Properties?.FirstOrDefault(p => p.Name == propName);
        }

        private MetadataHelper _helper = new MetadataHelper();

        private static Dictionary<string, string> GetNamespaceAliases(string xml)
        {
            var rv = new Dictionary<string, string>();
            try
            {
                var xmlRdr = XmlReader.Create(new StringReader(xml));
                bool result = true;
                while (result && xmlRdr.NodeType != XmlNodeType.Element)
                {
                    try
                    {
                        result = xmlRdr.Read();
                    }
                    catch
                    {
                        if (xmlRdr.NodeType != XmlNodeType.Element)
                            result = false;
                    }
                }

                if (result)
                {
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
            }
            catch
            {
                //
            }
            if (!rv.ContainsKey(""))
                rv[""] = MetadataUtils.AvaloniaNamespace;
            return rv;
        }

        public CompletionSet GetCompletions(AvMetadata metadata, string fullText, int pos, string currentAssemblyName = null)
        {
            string textToCursor = fullText.Substring(0, pos);
            _helper.SetMetadata(metadata, textToCursor, currentAssemblyName);

            if (_helper.Metadata == null)
                return null;

            if (fullText.Length == 0 || pos == 0)
                return null;
            var state = XmlParser.Parse(textToCursor);

            var completions = new List<AvCompletion>();

            int curStart = state.CurrentValueStart ?? 0;

            if (state.State == XmlParser.ParserState.StartElement)
            {
                var tagName = state.TagName;
                if (tagName.StartsWith("/"))
                {
                    if (textToCursor.Length < 2)
                        return null;
                    var closingState = XmlParser.Parse(textToCursor.Substring(0, textToCursor.Length - 2));

                    var name = closingState.GetParentTagName(0);
                    if (name == null)
                        return null;
                    completions.Add(new AvCompletion("/" + name + ">", CompletionKind.Class));
                }
                else if (tagName.Contains("."))
                {
                    var dotPos = tagName.IndexOf(".");
                    var typeName = tagName.Substring(0, dotPos);
                    var compName = tagName.Substring(dotPos + 1);
                    curStart = curStart + dotPos + 1;

                    var sameType = state.GetParentTagName(1) == typeName;

                    completions.AddRange(_helper.FilterPropertyNames(typeName, compName, attached: sameType ? (bool?)null : true, hasSetter: false)
                        .Select(p => new AvCompletion(p, sameType ? CompletionKind.Property : CompletionKind.AttachedProperty)));
                }
                else
                    completions.AddRange(_helper.FilterTypeNames(tagName).Select(x => new AvCompletion(x, CompletionKind.Class)));
            }
            else if (state.State == XmlParser.ParserState.InsideElement ||
                     state.State == XmlParser.ParserState.StartAttribute)
            {
                if (state.State == XmlParser.ParserState.InsideElement)
                    curStart = pos; //Force completion to be started from current cursor position

                string attributeSuffix = "=\"\"";
                int attributeOffset = 2;
                if (fullText.Length > pos && fullText[pos] == '=')
                {
                    // attribute already has value, we are editing name only
                    attributeSuffix = "";
                    attributeOffset = 0;
                }

                if (state.AttributeName?.Contains(".") == true)
                {
                    var dotPos = state.AttributeName.IndexOf('.');
                    curStart += dotPos + 1;
                    var split = state.AttributeName.Split(new[] { '.' }, 2);
                    completions.AddRange(_helper.FilterPropertyNames(split[0], split[1], attached: true, hasSetter: true)
                        .Select(x => new AvCompletion(x, x + attributeSuffix, x, CompletionKind.AttachedProperty, x.Length + attributeOffset)));

                    completions.AddRange(_helper.FilterEventNames(split[0], split[1], attached: true)
                        .Select(v => new AvCompletion(v, v + attributeSuffix, v, CompletionKind.AttachedEvent, v.Length + attributeOffset)));
                }
                else
                {
                    completions.AddRange(_helper.FilterPropertyNames(state.TagName, state.AttributeName, attached: false, hasSetter: true)
                        .Select(x => new AvCompletion(x, x + attributeSuffix, x, CompletionKind.Property, x.Length + attributeOffset)));

                    completions.AddRange(_helper.FilterEventNames(state.TagName, state.AttributeName, attached: false)
                        .Select(v => new AvCompletion(v, v + attributeSuffix, v, CompletionKind.Event, v.Length + attributeOffset)));

                    var targetType = _helper.LookupType(state.TagName);
                    completions.AddRange(
                        _helper.FilterTypes(state.AttributeName, xamlDirectiveOnly: true)
                            .Where(t => t.Value.IsValidForXamlContextFunc?.Invoke(currentAssemblyName, targetType, null) ?? true)
                            .Select(v => new AvCompletion(v.Key, v.Key + attributeSuffix, v.Key, CompletionKind.Class, v.Key.Length + attributeOffset)));

                    if (targetType?.IsAvaloniaObjectType == true)
                        completions.AddRange(
                            _helper.FilterTypeNames(state.AttributeName, withAttachedPropertiesOrEventsOnly: true)
                                .Select(v => new AvCompletion(v, v + ".", v, CompletionKind.Class)));
                }
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
                               BuildCompletionsForMarkupExtension(prop, completions, fullText, state,
                                   textToCursor.Substring(state.CurrentValueStart.Value), currentAssemblyName);
                }
                else
                {
                    prop = prop ?? _helper.LookupType(state.AttributeName)?.Properties.FirstOrDefault(p => p.Name == "");

                    if (prop?.Type?.HasHintValues == true)
                    {
                        var search = textToCursor.Substring(state.CurrentValueStart.Value);
                        if (prop.Type.IsCompositeValue)
                        {
                            var last = search.Split(' ', ',').LastOrDefault();
                            curStart = curStart + search.Length - last?.Length ?? 0;
                            search = last;

                            // Special case for pseudoclasses within the current edit
                            if (state.AttributeName.Equals("Selector") && search.Contains(":"))
                            {
                                search = ":";
                            }
                        }

                        completions.AddRange(GetHintCompletions(prop.Type, search, currentAssemblyName));
                    }
                    else if (prop?.Type?.Name == typeof(Type).FullName)
                    {
                        completions.AddRange(_helper.FilterTypeNames(state.AttributeValue).Select(x => new AvCompletion(x, x, x, CompletionKind.Class)));
                    }
                    else if (state.AttributeName == "xmlns" || state.AttributeName.Contains("xmlns:"))
                    {
                        if (state.AttributeValue.StartsWith("clr-namespace:"))
                            completions.AddRange(
                                metadata.Namespaces.Keys.Where(v => v.StartsWith(state.AttributeValue))
                                    .Select(v => new AvCompletion(v.Substring("clr-namespace:".Length), v, v, CompletionKind.Namespace)));
                        else
                        {
                            if ("using:".StartsWith(state.AttributeValue))
                                completions.Add(new AvCompletion("using:", CompletionKind.Namespace));

                            if ("clr-namespace:".StartsWith(state.AttributeValue))
                                completions.Add(new AvCompletion("clr-namespace:", CompletionKind.Namespace));

                            completions.AddRange(
                                metadata.Namespaces.Keys.Where(
                                    v =>
                                        v.StartsWith(state.AttributeValue) &&
                                        !v.StartsWith("clr-namespace"))
                                    .Select(v => new AvCompletion(v, CompletionKind.Namespace)));
                        }
                    }
                    else if (state.AttributeName.EndsWith(":Class"))
                    {
                        if (_helper.Aliases.TryGetValue(state.AttributeName.Replace(":Class", ""), out var ns) && ns == MetadataUtils.Xaml2006Namespace)
                        {
                            var asmKey = $";assembly={currentAssemblyName}";
                            var fullClassNames = _helper.Metadata.Namespaces.Where(v => v.Key.EndsWith(asmKey))
                                                                            .SelectMany(v => v.Value.Values.Where(t => t.IsAvaloniaObjectType))
                                                                            .Select(v => v.FullName);
                            completions.AddRange(
                                   fullClassNames
                                    .Where(v => v.StartsWith(state.AttributeValue))
                                    .Select(v => new AvCompletion(v, CompletionKind.Class)));
                        }
                    }
                    else if (state.TagName == "Setter" && (state.AttributeName == "Value" || state.AttributeName == "Property"))
                    {
                        ProcessStyleSetter(state.AttributeName, state, completions, currentAssemblyName);
                    }
                }
            }

            if (completions.Count != 0)
                return new CompletionSet() { Completions = completions, StartPosition = curStart };

            return null;
        }

        private void ProcessStyleSetter(string setterPropertyName, XmlParser state, List<AvCompletion> completions, string currentAssemblyName)
        {
            const string selectorTypes = @"(?<type>([\w|])+)|([:\.#/]\w+)";

            string stypeName = null;
            if (state.GetParentTagName(1)?.Equals("ControlTheme") == true)
            {
                stypeName = state.FindParentAttributeValue("TargetType", 1, maxLevels: 0);
            }
            else
            {
                var selector = state.FindParentAttributeValue("Selector", 1, maxLevels: 0);
                var matches = Regex.Matches(selector ?? "", selectorTypes);
                var types = matches.OfType<Match>().Select(m => m.Groups["type"].Value).Where(v => !string.IsNullOrEmpty(v));
                stypeName = types.LastOrDefault()?.Replace('|', ':') ?? "Control";
            }

            if (string.IsNullOrEmpty(stypeName))
                return;

            if (setterPropertyName == "Property")
            {
                string value = state.AttributeValue ?? "";

                if (value.Contains("."))
                {
                    int curStart = state.CurrentValueStart ?? 0;
                    var dotPos = value.IndexOf(".");
                    var typeName = value.Substring(0, dotPos);
                    var compName = value.Substring(dotPos + 1);
                    curStart = curStart + dotPos + 1;

                    var sameType = state.GetParentTagName(1) == typeName;

                    completions.AddRange(_helper.FilterPropertyNames(typeName, compName, attached: true, hasSetter: true)
                                    .Select(p => new AvCompletion(p, $"{typeName}.{p}", p, CompletionKind.AttachedProperty)));
                }
                else
                {
                    completions.AddRange(_helper.FilterPropertyNames(stypeName, value, attached: false, hasSetter: true)
                            .Select(x => new AvCompletion(x, CompletionKind.Property)));

                    completions.AddRange(_helper.FilterTypeNames(value, withAttachedPropertiesOrEventsOnly: true).Select(x => new AvCompletion(x, CompletionKind.Class)));
                }

            }
            else if (setterPropertyName == "Value")
            {
                var setterProperty = state.FindParentAttributeValue("Property", maxLevels: 0);

                if (setterProperty.Contains("."))
                {
                    var vals = setterProperty.Split('.');
                    stypeName = vals[0];
                    setterProperty = vals[1];
                }

                var setterProp = _helper.LookupProperty(stypeName, setterProperty);
                if (setterProp?.Type?.HasHintValues == true)
                {
                    completions.AddRange(GetHintCompletions(setterProp.Type, state.AttributeValue, currentAssemblyName));
                }
            }

            //bool isControlTheme()
            //{
            //    var parentTag = state.GetParentTagName(state.NestingLevel - 1);
                
            //    return parentTag?.Equals("ControlTheme") ?? false;
            //}
        }

        public IEnumerable<string> FilterHintValues(MetadataType type, string entered, string currentAssemblyName, XmlParser state)
        {
            entered = entered ?? "";

            if (type == null)
                yield break;

            if (!string.IsNullOrEmpty(currentAssemblyName) && type.XamlContextHintValuesFunc != null)
            {
                foreach (var v in type.XamlContextHintValuesFunc(currentAssemblyName, type, null).Where(v => v.StartsWith(entered, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return v;
                }
            }

            foreach (var v in type.HintValues.Where(v => v.StartsWith(entered, StringComparison.OrdinalIgnoreCase)))
            {
                yield return v;
            }
        }

        private IEnumerable<AvCompletion> FilterHintValuesForBindingPath(MetadataType bindingPathType, string entered, string currentAssemblyName, string fullText, XmlParser state)
        {
            IEnumerable<AvCompletion> forPropertiesFromType(MetadataType filterType, string filter, Func<string, string> fmtInsertText = null)
            {
                if (filterType != null)
                {
                    foreach (var propertyName in _helper.FilterPropertyNames(filterType, filter, false, false))
                    {
                        yield return new AvCompletion(propertyName, fmtInsertText?.Invoke(propertyName) ?? propertyName, propertyName, CompletionKind.Property);
                    }
                }
            }

            IEnumerable<AvCompletion> forProperties(string filterType, string filter, Func<string, string> fmtInsertText = null)
                    => forPropertiesFromType(_helper.LookupType(filterType ?? ""), filter, fmtInsertText);

            if (string.IsNullOrEmpty(entered))
                return forProperties(state.FindParentAttributeValue("(x\\:)?DataType"), entered);

            var values = entered.Split('.');

            if (values.Length == 1)
            {
                if (values[0].StartsWith("$parent["))
                {
                    return _helper.FilterTypes(entered.Substring("$parent[".Length))
                        .Select(v => new AvCompletion(v.Key, $"$parent[{v.Key}].", v.Key, CompletionKind.Class));
                }
                else if (values[0].StartsWith("#"))
                {
                    var nameMatch = Regex.Matches(fullText, $"\\s(?:(x\\:)?Name)=\"(?<AttribValue>[\\w\\:\\s\\|\\.]+)\"");

                    if (nameMatch.Count > 0)
                    {
                        var result = new List<AvCompletion>();
                        foreach (Match m in nameMatch)
                        {
                            if (m.Success)
                            {
                                var name = m.Groups["AttribValue"].Value;
                                result.Add(new AvCompletion(name, $"#{name}", name, CompletionKind.Class));
                            }
                        }
                        return result;
                    }

                    return Array.Empty<AvCompletion>();
                }

                return forProperties(state.FindParentAttributeValue("(x\\:)?DataType"), entered);
            }

            string type = values[0];

            int i;

            if (values[0].StartsWith("$"))
            {
                i = 1;
                type = "Control";
                if (values[0] == "$self") //current control type
                    type = state.GetParentTagName(0);
                else if (values[0] == "$parent") //parent control in the xaml
                    type = state.GetParentTagName(1) ?? "Control";
                else if (values[0].StartsWith("$parent[")) //extract parent type
                    type = values[0].Substring("$parent[".Length, values[0].Length - "$parent[".Length - 1);
            }
            else if (values[0].StartsWith("#"))
            {
                i = 1;
                //todo: find the control type etc ???
                type = "Control";
            }
            else
            {
                i = 0;
                type = state.FindParentAttributeValue("(x\\:)?DataType");
            }

            var mdType = _helper.LookupType(type ?? "");

            while (mdType != null && i < values.Length - 1 && !string.IsNullOrEmpty(values[i]))
            {
                if (i <= 1 && values[i] == "DataContext")
                {
                    //assume parent.datacontext is x:datatype so we have some intelisence
                    type = state.FindParentAttributeValue("(x\\:)?DataType");
                    mdType = _helper.LookupType(type);
                }
                else
                {
                    mdType = mdType.Properties.FirstOrDefault(p => p.Name == values[i])?.Type;
                    type = mdType?.FullName;
                }
                i++;
            }

            return forPropertiesFromType(mdType, values[i], p => $"{string.Join(".", values.Take(i).ToArray())}.{p}");
        }

        private List<AvCompletion> GetHintCompletions(MetadataType type, string entered, string currentAssemblyName = null, string fullText = null, XmlParser state = null)
        {
            var kind = GetCompletionKindForHintValues(type);

            var completions = FilterHintValues(type, entered, currentAssemblyName, state)
                .Select(val => new AvCompletion(val, kind)).ToList();

            if (type.FullName == "{BindingPath}" && state != null)
            {
                completions.AddRange(FilterHintValuesForBindingPath(type, entered, currentAssemblyName, fullText, state));
            }
            return completions;
        }

        private int BuildCompletionsForMarkupExtension(MetadataProperty property, List<AvCompletion> completions, string fullText, XmlParser state, string data, string currentAssemblyName)
        {
            int? forcedStart = null;
            var ext = MarkupExtensionParser.Parse(data);

            var transformedName = (ext.ElementName ?? "").Trim();
            if (_helper.LookupType(transformedName)?.IsMarkupExtension != true)
                transformedName += "Extension";

            if (ext.State == MarkupExtensionParser.ParserStateType.StartElement)
                completions.AddRange(_helper.FilterTypeNames(ext.ElementName, markupExtensionsOnly: true)
                    .Select(t => t.EndsWith("Extension") ? t.Substring(0, t.Length - "Extension".Length) : t)
                    .Select(t => new AvCompletion(t, CompletionKind.MarkupExtension)));
            if (ext.State == MarkupExtensionParser.ParserStateType.StartAttribute ||
                ext.State == MarkupExtensionParser.ParserStateType.InsideElement)
            {
                if (ext.State == MarkupExtensionParser.ParserStateType.InsideElement)
                    forcedStart = data.Length;

                completions.AddRange(_helper.FilterPropertyNames(transformedName, ext.AttributeName ?? "", attached: false, hasSetter: true)
                    .Select(x => new AvCompletion(x, x + "=", x, CompletionKind.Property)));

                var attribName = ext.AttributeName ?? "";
                var t = _helper.LookupType(transformedName);

                bool ctorArgument = ext.AttributesCount == 0;
                //skip ctor hints when some property is already set
                if (t != null && t.IsMarkupExtension && t.SupportCtorArgument != MetadataTypeCtorArgument.None && ctorArgument)
                {
                    if (t.SupportCtorArgument == MetadataTypeCtorArgument.HintValues)
                    {
                        if (t.HasHintValues)
                        {
                            completions.AddRange(GetHintCompletions(t, attribName));
                        }
                    }
                    else if (attribName.Contains("."))
                    {
                        if (t.SupportCtorArgument != MetadataTypeCtorArgument.Type)
                        {
                            var split = attribName.Split('.');
                            var type = split[0];
                            var prop = split[1];

                            var mType = _helper.LookupType(type);
                            if (mType != null && t.SupportCtorArgument == MetadataTypeCtorArgument.HintValues)
                            {
                                var hints = FilterHintValues(mType, prop, currentAssemblyName, state);
                                completions.AddRange(hints.Select(x => new AvCompletion(x, $"{type}.{x}", x, GetCompletionKindForHintValues(mType))));
                            }

                            var props = _helper.FilterPropertyNames(type, prop, attached: false, hasSetter: false, staticGetter: true);
                            completions.AddRange(props.Select(x => new AvCompletion(x, $"{type}.{x}", x, CompletionKind.StaticProperty)));
                        }
                    }
                    else
                    {
                        var types = _helper.FilterTypeNames(attribName,
                            staticGettersOnly: t.SupportCtorArgument == MetadataTypeCtorArgument.Object);

                        completions.AddRange(types.Select(x => new AvCompletion(x, x, x, CompletionKind.Class)));

                        if (property?.Type?.HasHintValues == true)
                        {
                            completions.Add(new AvCompletion(property.Type.Name, property.Type.Name + ".", property.Type.Name, CompletionKind.Class));
                        }
                    }
                }
                else
                {
                    var defaultProp = t?.Properties.FirstOrDefault(p => p.Name == "");
                    if (defaultProp?.Type?.HasHintValues ?? false)
                    {
                        completions.AddRange(GetHintCompletions(defaultProp.Type, ext.AttributeName ?? "", currentAssemblyName, fullText, state));
                    }
                }
            }
            if (ext.State == MarkupExtensionParser.ParserStateType.AttributeValue
                || ext.State == MarkupExtensionParser.ParserStateType.BeforeAttributeValue)
            {
                var prop = _helper.LookupProperty(transformedName, ext.AttributeName);
                if (prop?.Type?.HasHintValues == true)
                {
                    var start = data.Substring(ext.CurrentValueStart);
                    completions.AddRange(GetHintCompletions(prop.Type, start, currentAssemblyName, fullText, state));
                }
            }

            return forcedStart ?? ext.CurrentValueStart;
        }

        public static bool ShouldTriggerCompletionListOn(char typedChar)
        {
            return char.IsLetterOrDigit(typedChar) || typedChar == '/' || typedChar == '<'
                || typedChar == ' ' || typedChar == '.' || typedChar == ':' || typedChar == '$' 
                || typedChar == '#' || typedChar == '-' || typedChar == '^';
        }

        public static CompletionKind GetCompletionKindForHintValues(MetadataType type)
            => type.IsEnum ? CompletionKind.Enum : CompletionKind.StaticProperty;
    }
}
