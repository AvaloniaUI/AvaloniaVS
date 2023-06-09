#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Avalonia.Ide.CompletionEngine;

public class CompletionEngine
{
    private class MetadataHelper
    {
        private Metadata? _metadata;
        public Metadata? Metadata => _metadata;
        public Dictionary<string, string>? Aliases { get; private set; }

        private Dictionary<string, MetadataType>? _types;
        private string? _currentAssemblyName;
        private static Regex? _findElementByNameRegex;
        internal static Regex FindElementByNameRegex => _findElementByNameRegex ??=
             new($"\\s(?:(x\\:)?Name)=\"(?<AttribValue>[\\w\\:\\s\\|\\.]+)\"", RegexOptions.Compiled);

        public void SetMetadata(Metadata metadata, string xml, string? currentAssemblyName = null)
        {
            var aliases = GetNamespaceAliases(xml);

            //Check if metadata and aliases can be reused
            if (_metadata == metadata && Aliases != null && _types != null && currentAssemblyName == _currentAssemblyName)
            {
                if (aliases.Count == Aliases.Count)
                {
                    var mismatch = false;
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
                var aliasValue = alias.Value ?? "";

                if (!string.IsNullOrEmpty(_currentAssemblyName) && aliasValue.StartsWith("clr-namespace:") && !aliasValue.Contains(";assembly="))
                    aliasValue = $"{aliasValue};assembly={_currentAssemblyName}";

                if (!metadata.Namespaces.TryGetValue(aliasValue, out var ns))
                    continue;

                var prefix = alias.Key.Length == 0 ? "" : alias.Key + ":";
                foreach (var type in ns.Values)
                    types[prefix + type.Name] = type;
            }

            _types = types;
        }

        public IEnumerable<KeyValuePair<string, MetadataType>> FilterTypes(string? prefix, bool withAttachedPropertiesOrEventsOnly = false, bool markupExtensionsOnly = false, bool staticGettersOnly = false, bool xamlDirectiveOnly = false)
        {
            if (_types is null)
            {
                return Array.Empty<KeyValuePair<string, MetadataType>>();
            }

            prefix ??= "";

            var e = _types
                .Where(t => t.Value.IsXamlDirective == xamlDirectiveOnly && t.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.Key.Equals("ControlTemplateResult") && !x.Key.Equals("DataTemplateExtensions"));
            if (withAttachedPropertiesOrEventsOnly)
                e = e.Where(t => t.Value.HasAttachedProperties || t.Value.HasAttachedEvents);
            if (markupExtensionsOnly)
                e = e.Where(t => t.Value.IsMarkupExtension);
            if (staticGettersOnly)
                e = e.Where(t => t.Value.HasStaticGetProperties);

            return e;
        }

        public IEnumerable<string> FilterTypeNames(string? prefix, bool withAttachedPropertiesOrEventsOnly = false, bool markupExtensionsOnly = false, bool staticGettersOnly = false, bool xamlDirectiveOnly = false)
        {
            return FilterTypes(prefix, withAttachedPropertiesOrEventsOnly, markupExtensionsOnly, staticGettersOnly, xamlDirectiveOnly).Select(s => s.Key);
        }

        public MetadataType? LookupType(string? name)
        {
            if (name is null)
            {
                return null;
            }

            MetadataType? rv = null;
            if (!(_types?.TryGetValue(name, out rv) == true))
            {
                // Markup extensions used as XML elements will fail to lookup because
                // the tag name won't include 'Extension'
                _types?.TryGetValue($"{name}Extension", out rv);
            }
            return rv;
        }

        public IEnumerable<string> FilterPropertyNames(string typeName, string? propName,
            bool? attached,
            bool hasSetter,
            bool staticGetter = false)
        {
            var t = LookupType(typeName);
            return MetadataHelper.FilterPropertyNames(t, propName, attached, hasSetter, staticGetter);
        }

        public static IEnumerable<string> FilterPropertyNames(MetadataType? t,
            string? propName,
            bool? attached,
            bool hasSetter,
            bool staticGetter = false)
        {
            return FilterProperty(t, propName, attached, hasSetter, staticGetter).Select(p => p.Name);
        }

        public static IEnumerable<MetadataProperty> FilterProperty(MetadataType? t, string? propName,
            bool? attached,
            bool hasSetter,
            bool staticGetter = false
            )
        {
            propName ??= "";
            if (t == null)
                return Array.Empty<MetadataProperty>();

            var e = t.Properties.Where(p => p.Name.StartsWith(propName, StringComparison.OrdinalIgnoreCase) && (hasSetter ? p.HasSetter : p.HasGetter));

            if (attached.HasValue)
                e = e.Where(p => p.IsAttached == attached);
            if (staticGetter)
                e = e.Where(p => p.IsStatic && p.HasGetter);
            else
                e = e.Where(p => !p.IsStatic);

            return e;
        }


        public IEnumerable<string> FilterEventNames(string typeName, string? propName,
            bool attached)
        {
            var t = LookupType(typeName);
            propName ??= "";
            if (t == null)
                return Array.Empty<string>();

            return t.Events.Where(n => n.IsAttached == attached && n.Name.StartsWith(propName)).Select(n => n.Name);
        }

        public MetadataProperty? LookupProperty(string? typeName, string? propName)
            => LookupType(typeName)?.Properties?.FirstOrDefault(p => p.Name == propName);
    }

    private readonly MetadataHelper _helper = new();

    private static Dictionary<string, string> GetNamespaceAliases(string xml)
    {
        var rv = new Dictionary<string, string>();
        try
        {
            var xmlRdr = XmlReader.Create(new StringReader(xml));
            var result = true;
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
            rv[""] = Utils.AvaloniaNamespace;
        return rv;
    }

    public CompletionSet? GetCompletions(Metadata metadata, string fullText, int pos, string? currentAssemblyName = null)
    {
        var textToCursor = fullText.Substring(0, pos);
        _helper.SetMetadata(metadata, textToCursor, currentAssemblyName);

        if (_helper.Metadata == null)
            return null;

        if (fullText.Length == 0 || pos == 0)
            return null;
        var state = XmlParser.Parse(textToCursor);

        var completions = new List<Completion>();

        var curStart = state.CurrentValueStart ?? 0;

        if (state.State == XmlParser.ParserState.StartElement)
        {
            var tagName = state.TagName;
            if (tagName is null)
            {
            }
            else if (tagName.StartsWith("/"))
            {
                if (textToCursor.Length < 2)
                    return null;
                var closingState = XmlParser.Parse(textToCursor.Substring(0, textToCursor.Length - 2));

                var name = closingState.GetParentTagName(0);
                if (name == null)
                    return null;
                completions.Add(new Completion("/" + name + ">", CompletionKind.Class));
            }
            else if (tagName.Contains('.'))
            {
                var dotPos = tagName.IndexOf(".");
                var typeName = tagName.Substring(0, dotPos);
                var compName = tagName.Substring(dotPos + 1);
                curStart = curStart + dotPos + 1;

                var sameType = state.GetParentTagName(1) == typeName;

                completions.AddRange(_helper.FilterPropertyNames(typeName, compName, attached: sameType ? (bool?)null : true, hasSetter: false)
                    .Select(p => new Completion(p, sameType ? CompletionKind.Property : CompletionKind.AttachedProperty)));
            }
            else
            {
                completions.AddRange(_helper.FilterTypes(tagName).Select(kvp =>
                {
                    if (kvp.Value.IsMarkupExtension)
                    {
                        var xamlName = kvp.Key.Substring(0, kvp.Key.Length - 9 /* length of "extension" */);
                        return new Completion(xamlName, CompletionKind.Class);
                    }

                    return new Completion(kvp.Key, CompletionKind.Class);
                }));
            }
        }
        else if (state.State == XmlParser.ParserState.InsideElement ||
                 state.State == XmlParser.ParserState.StartAttribute)
        {
            if (state.State == XmlParser.ParserState.InsideElement)
                curStart = pos; //Force completion to be started from current cursor position

            var attributeSuffix = "=\"\"";
            var attributeOffset = 2;
            if (fullText.Length > pos && fullText[pos] == '=')
            {
                // attribute already has value, we are editing name only
                attributeSuffix = "";
                attributeOffset = 0;
            }
            var attributeName = state.AttributeName;
            if (attributeName?.Contains('.') == true)
            {
                var dotPos = attributeName.IndexOf('.');
                curStart += dotPos + 1;
                var split = attributeName.Split(new[] { '.' }, 2);
                completions.AddRange(_helper.FilterPropertyNames(split[0], split[1], attached: true, hasSetter: true)
                    .Select(x => new Completion(x, x + attributeSuffix, x, CompletionKind.AttachedProperty, x.Length + attributeOffset)));

                completions.AddRange(_helper.FilterEventNames(split[0], split[1], attached: true)
                    .Select(v => new Completion(v, v + attributeSuffix, v, CompletionKind.AttachedEvent, v.Length + attributeOffset)));
            }
            else if (state.TagName is not null)
            {
                completions.AddRange(_helper.FilterPropertyNames(state.TagName, attributeName, attached: false, hasSetter: true)
                    .Select(x => new Completion(x, x + attributeSuffix, x, CompletionKind.Property, x.Length + attributeOffset)));

                // Special case for "<On " here, 'Options' property is get only list property
                // which is skipped above - Add it back here
                // Future TODO: The metadata probably needs to adapt for this, but this opens up
                // potential issues with readonly properties that we don't want visible, so leaving
                // this up to be dealt with in the future
                if (state.TagName.Equals("On"))
                {
                    completions.Add(new Completion("Options", "Options=\"\"", "Options",
                        CompletionKind.Property, 9 /*recommendedCursorOffset*/));
                }

                completions.AddRange(_helper.FilterEventNames(state.TagName, attributeName, attached: false)
                    .Select(v => new Completion(v, v + attributeSuffix, v, CompletionKind.Event, v.Length + attributeOffset)));

                var targetType = _helper.LookupType(state.TagName);
                if (targetType is not null)
                {
                    completions.AddRange(
                        _helper.FilterTypes(attributeName, xamlDirectiveOnly: true)
                            .Where(t => t.Value.IsValidForXamlContextFunc?.Invoke(currentAssemblyName, targetType, null) ?? true)
                            .Select(v => new Completion(v.Key, v.Key + attributeSuffix, v.Key, CompletionKind.Class, v.Key.Length + attributeOffset)));

                    if (targetType.IsAvaloniaObjectType)
                    {
                        if (string.IsNullOrEmpty(attributeName) || "xmlns".StartsWith(attributeName,StringComparison.OrdinalIgnoreCase))
                        {
                            completions.Add(new("xmlns:", CompletionKind.Class));
                        }
                        completions.AddRange(
                            _helper.FilterTypeNames(attributeName, withAttachedPropertiesOrEventsOnly: true)
                                .Select(v => new Completion(v, v + ".", v, CompletionKind.Class)));
                    }
                }
            }
        }
        else if (state.State == XmlParser.ParserState.AttributeValue)
        {
            MetadataProperty? prop = null;
            if (state.AttributeName?.Contains('.') == true)
            {
                //Attached property
                var split = state.AttributeName.Split('.');
                prop = _helper.LookupProperty(split[0], split[1]);
            }
            else if (state.TagName is not null)
                prop = _helper.LookupProperty(state.TagName, state.AttributeName);

            //Markup extension, ignore everything else
            if (state.AttributeValue?.StartsWith("{") == true && state.CurrentValueStart.HasValue)
            {
                curStart = state.CurrentValueStart.Value +
                           BuildCompletionsForMarkupExtension(prop, completions, fullText, state,
                               textToCursor.Substring(state.CurrentValueStart.Value), currentAssemblyName);
            }
            else
            {
                prop ??= _helper.LookupType(state.AttributeName)?.Properties.FirstOrDefault(p => string.IsNullOrEmpty(p.Name));

                if (prop?.Type?.HasHintValues == true && state.CurrentValueStart.HasValue)
                {
                    var search = textToCursor.Substring(state.CurrentValueStart.Value);
                    var hintCompletions = true;
                    if (prop.Type.IsCompositeValue)
                    {
                        // Special case for pseudoclasses within the current edit
                        if (state.AttributeName!.Equals("Selector"))
                        {
                            hintCompletions = false;
                            if (ProcesssSelector(search.AsSpan(), state, completions, currentAssemblyName, fullText) is int delta)
                            {
                                curStart = curStart + delta;
                            }
                        }
                        else
                        {
                            var last = search.Split(' ', ',').Last();
                            search = last;
                            curStart = curStart + search.Length - last?.Length ?? 0;
                        }
                    }
                    if (hintCompletions)
                    {
                        completions.AddRange(GetHintCompletions(prop.Type, search, currentAssemblyName));
                    }
                }
                else if (prop?.Type?.Name == typeof(Type).FullName)
                {
                    var cKind = CompletionKind.Class;
                    if (state?.AttributeName?.Equals("TargetType") == true ||
                        state?.AttributeName?.Equals("Selector") == true)
                    {
                        cKind |= CompletionKind.TargetTypeClass;
                    }

                    completions.AddRange(_helper.FilterTypeNames(state?.AttributeValue)
                        .Select(x => new Completion(x, x, x, cKind)));
                }
                else if ((state.AttributeName == "xmlns" || state.AttributeName?.Contains("xmlns:") == true)
                    && state.AttributeValue is not null)
                {
                    IEnumerable<string> filterNamespaces(Func<string, bool> predicate)
                    {
                        var result = metadata.Namespaces.Keys.Where(predicate).ToList();

                        result.Sort((x, y) => x.CompareTo(y));

                        return result;
                    }

                    var cKind = CompletionKind.Namespace | CompletionKind.VS_XMLNS;

                    if (state.AttributeValue.StartsWith("clr-namespace:"))
                        completions.AddRange(
                                filterNamespaces(v => v.StartsWith(state.AttributeValue))
                                .Select(v => new Completion(v.Substring("clr-namespace:".Length), v, v, cKind)));
                    else
                    {
                        if ("using:".StartsWith(state.AttributeValue))
                            completions.Add(new Completion("using:", cKind));

                        if ("clr-namespace:".StartsWith(state.AttributeValue))
                            completions.Add(new Completion("clr-namespace:", cKind));

                        completions.AddRange(
                            filterNamespaces(
                                v =>
                                    v.StartsWith(state.AttributeValue) &&
                                    !v.StartsWith("clr-namespace"))
                                .Select(v => new Completion(v, cKind)));
                    }
                }
                else if (state.AttributeName?.EndsWith(":Class") == true && state.AttributeValue is not null)
                {
                    if (_helper.Aliases?.TryGetValue(state.AttributeName.Replace(":Class", ""), out var ns) == true && ns == Utils.Xaml2006Namespace)
                    {
                        var asmKey = $";assembly={currentAssemblyName}";
                        var fullClassNames = _helper.Metadata.Namespaces.Where(v => v.Key.EndsWith(asmKey))
                                                                        .SelectMany(v => v.Value.Values.Where(t => t.IsAvaloniaObjectType))
                                                                        .Select(v => v.FullName);
                        completions.AddRange(
                               fullClassNames
                                .Where(v => v.StartsWith(state.AttributeValue))
                                .Select(v => new Completion(v, CompletionKind.Class | CompletionKind.TargetTypeClass)));
                    }
                }
                else if (state.TagName == "Setter" && (state.AttributeName == "Value" || state.AttributeName == "Property"))
                {
                    ProcessStyleSetter(state.AttributeName, state, completions, currentAssemblyName);

                    bool isAttached = textToCursor.AsSpan().Slice(curStart, pos - curStart).IndexOf('.') != -1;
                    if (isAttached)
                        curStart = pos;
                }
                else if (state.TagName == "On")
                {
                    if (state?.AttributeName?.Equals("Options") == true)
                    {
                        var parentTag = state.GetParentTagName(1);
                        if (parentTag?.Equals("OnPlatform") == true)
                        {
                            // Built in types from:
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/src/Markup/Avalonia.Markup.Xaml/MarkupExtensions/OnPlatformExtension.cs
                            completions.Add(new Completion("Windows", CompletionKind.Enum));
                            completions.Add(new Completion("macOS", CompletionKind.Enum));
                            completions.Add(new Completion("Linux", CompletionKind.Enum));
                            completions.Add(new Completion("Android", CompletionKind.Enum));
                            completions.Add(new Completion("IOS", CompletionKind.Enum));
                            completions.Add(new Completion("Browser", CompletionKind.Enum));
                        }
                        else if (parentTag?.Equals("OnFormFactor") == true)
                        {
                            completions.Add(new Completion("Desktop", CompletionKind.Enum));
                            completions.Add(new Completion("Mobile", CompletionKind.Enum));
                        }
                    }
                    else if (state?.AttributeName?.Equals("Content") == true)
                    {
                        // For content, lets find the completions relevant to the property
                        var propertyTag = state.GetParentTagName(2)!;
                        var dotPos = propertyTag.IndexOf(".");
                        var typeName = propertyTag.Substring(0, dotPos);
                        var compName = propertyTag.Substring(dotPos + 1);

                        var property = _helper.LookupProperty(typeName, compName);

                        if (property?.Type?.HasHintValues == true)
                        {
                            completions.AddRange(GetHintCompletions(property.Type, null, currentAssemblyName));
                        }
                    }
                }
            }
        }

        if (completions.Count != 0)
            return new CompletionSet() { Completions = SortCompletions(completions), StartPosition = curStart };

        return null;
    }

    private static List<Completion> SortCompletions(List<Completion> completions)
    {
        // Group the completions based on Kind, and sort the completions for each group
        return completions
            .GroupBy(i => i.Kind, (kind, compl) =>
                (Kind: kind, Completions: compl.OrderBy(j => j.DisplayText)))
            .OrderBy(i => GetCompletionPriority(i.Kind))
            .SelectMany(i => i.Completions)
            .ToList();
    }

    private static int GetCompletionPriority(CompletionKind kind)
    {
        return kind switch
        {
            CompletionKind.MarkupExtension => 0,
            CompletionKind.Namespace => 1,
            CompletionKind.Property => 2,
            CompletionKind.AttachedProperty => 3,
            CompletionKind.StaticProperty => 4,
            CompletionKind.Event => 5,
            CompletionKind.AttachedEvent => 6,
            CompletionKind.Class => 7,
            CompletionKind.Enum => 8,
            CompletionKind.None => 9,
            _ => (int)kind
        };
    }

    private void ProcessStyleSetter(string setterPropertyName, XmlParser state, List<Completion> completions, string? currentAssemblyName)
    {
        const string selectorTypes = @"(?<type>([\w|])+)|([:\.#/]\w+)";

        // TODO: This improves ControlThemes to properly suggest properties in Setters,
        // but we still need to improve this for nested Styles:
        // <Style Selector="^:pointerover">
        // Won't show suggestions (or incorrect ones), because the else clause below will fail
        // to find a type in that selector and we don't search up the Xml tree
        string? selectorTypeName = null;
        if (state.GetParentTagName(1)?.Equals("ControlTheme") == true)
        {
            selectorTypeName = state.FindParentAttributeValue("TargetType", 1, maxLevels: 0);
        }
        else
        {
            var selector = state.FindParentAttributeValue("Selector", 1, maxLevels: 0);
            var matches = Regex.Matches(selector ?? "", selectorTypes);
            var types = matches.OfType<Match>().Select(m => m.Groups["type"].Value).Where(v => !string.IsNullOrEmpty(v));
            selectorTypeName = types.LastOrDefault()?.Replace('|', ':') ?? "Control";
        }

        if (string.IsNullOrEmpty(selectorTypeName))
            return;

        if (setterPropertyName == "Property")
        {
            var value = state.AttributeValue ?? "";

            if (value.Contains('.'))
            {
                var curStart = state.CurrentValueStart ?? 0;
                var dotPos = value.IndexOf(".");
                var typeName = value.Substring(0, dotPos);
                var compName = value.Substring(dotPos + 1);
                curStart = curStart + dotPos + 1;

                var sameType = state.GetParentTagName(1) == typeName;

                completions.AddRange(_helper.FilterPropertyNames(typeName, compName, attached: true, hasSetter: true)
                                .Select(p => new Completion(p, p, p, CompletionKind.AttachedProperty)));
            }
            else
            {
                completions.AddRange(_helper.FilterPropertyNames(selectorTypeName, value, attached: false, hasSetter: true)
                        .Select(x => new Completion(x, CompletionKind.Property)));

                completions.AddRange(_helper.FilterTypeNames(value, withAttachedPropertiesOrEventsOnly: true).Select(x => new Completion(x, CompletionKind.Class)));
            }

        }
        else if (setterPropertyName == "Value")
        {
            var setterProperty = state.FindParentAttributeValue("Property", maxLevels: 0);
            if (setterProperty is not null)
            {
                if (setterProperty.Contains('.') == true)
                {
                    var vals = setterProperty.Split('.');
                    selectorTypeName = vals[0];
                    setterProperty = vals[1];
                }

                var setterProp = _helper.LookupProperty(selectorTypeName, setterProperty);
                if (setterProp?.Type?.HasHintValues == true && state.AttributeValue is not null)
                {
                    completions.AddRange(GetHintCompletions(setterProp.Type, state.AttributeValue, currentAssemblyName));
                }
            }
        }
    }

    public IEnumerable<string> FilterHintValues(MetadataType type, string? entered, string? currentAssemblyName, XmlParser? state)
    {
        entered ??= "";

        if (type == null)
            yield break;

        if (!string.IsNullOrEmpty(currentAssemblyName) && type.XamlContextHintValuesFunc != null)
        {
            foreach (var v in type.XamlContextHintValuesFunc(currentAssemblyName, type, null).Where(v => v.StartsWith(entered, StringComparison.OrdinalIgnoreCase)))
            {
                yield return v;
            }
        }

        if (type.HintValues is not null)
        {
            // Don't filter values here by 'StartsWith' (old behavior), provide all the hints,
            // For VS, Intellisense will filter the results for us, other users of the completion
            // engine (outside of VS) will need to filter later
            // Otherwise, in VS, it's impossible to get the full list for something like brushes:
            // Background="Red" -> Background="B", will only populate with the 'B' brushes and hitting
            // backspace after that will keep the 'B' brushes only instead of showing the whole list
            // WPF/UWP loads the full list of brushes and highlights starting at the B and then
            // filters the list down from there - otherwise its difficult to keep the completion list
            // and see all choices if making edits
            foreach (var v in type.HintValues)
            {
                yield return v;
            }
        }
    }

    private IEnumerable<Completion> FilterHintValuesForBindingPath(MetadataType bindingPathType, string? entered, string? currentAssemblyName, string? fullText, XmlParser state)
    {
        IEnumerable<Completion> forPropertiesFromType(MetadataType? filterType, string? filter, Func<string, string>? fmtInsertText = null)
        {
            if (filterType != null)
            {
                foreach (var propertyName in MetadataHelper.FilterPropertyNames(filterType, filter, false, false))
                {
                    yield return new Completion(propertyName, fmtInsertText?.Invoke(propertyName) ?? propertyName, propertyName, CompletionKind.DataProperty);
                }
            }
        }

        IEnumerable<Completion> forProperties(string? filterType, string? filter, Func<string, string>? fmtInsertText = null)
                => forPropertiesFromType(_helper.LookupType(filterType ?? ""), filter, fmtInsertText);

        if (string.IsNullOrEmpty(entered))
            return forProperties(state.FindParentAttributeValue("(x\\:)?DataType"), entered);

        var values = entered.Split('.');

        if (values.Length == 1)
        {
            if (values[0].StartsWith("$parent["))
            {
                return _helper.FilterTypes(entered.Substring("$parent[".Length))
                    .Select(v => new Completion(v.Key, $"$parent[{v.Key}].", v.Key, CompletionKind.Class));
            }
            else if (values[0].StartsWith("#"))
            {
                if (fullText is not null)
                {
                    var nameMatch = MetadataHelper.FindElementByNameRegex.Matches(fullText);
                    if (nameMatch is { Count: > 0 })
                    {
                        var result = new List<Completion>();
                        foreach (Match m in nameMatch)
                        {
                            if (m.Success)
                            {
                                var name = m.Groups["AttribValue"].Value;
                                result.Add(new Completion(name, $"#{name}", name, CompletionKind.Class));
                            }
                        }
                        return result;
                    }
                }

                return Array.Empty<Completion>();
            }

            return forProperties(state.FindParentAttributeValue("(x\\:)?DataType"), entered);
        }

        var type = values[0];

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
                mdType = type is not null ? _helper.LookupType(type) : null;
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

    private List<Completion> GetHintCompletions(MetadataType type, string? entered, string? currentAssemblyName = null, string? fullText = null, XmlParser? state = null)
    {
        var kind = GetCompletionKindForHintValues(type);

        var completions = FilterHintValues(type, entered, currentAssemblyName, state)
            .Select(val => new Completion(val, kind)).ToList();

        if (type.FullName == "{BindingPath}" && state != null)
        {
            completions.AddRange(FilterHintValuesForBindingPath(type, entered, currentAssemblyName, fullText, state));
        }
        return completions;
    }

    private int BuildCompletionsForMarkupExtension(MetadataProperty? property, List<Completion> completions, string fullText, XmlParser state, string data, string? currentAssemblyName)
    {
        int? forcedStart = null;
        var ext = MarkupExtensionParser.Parse(data);

        var transformedName = (ext.ElementName ?? "").Trim();
        if (_helper.LookupType(transformedName)?.IsMarkupExtension != true)
            transformedName += "Extension";

        if (ext.State == MarkupExtensionParser.ParserStateType.StartElement)
            completions.AddRange(_helper.FilterTypeNames(ext.ElementName, markupExtensionsOnly: true)
                .Select(t => t.EndsWith("Extension") ? t.Substring(0, t.Length - "Extension".Length) : t)
                .Select(t => new Completion(t, CompletionKind.MarkupExtension)));
        if (ext.State == MarkupExtensionParser.ParserStateType.StartAttribute ||
            ext.State == MarkupExtensionParser.ParserStateType.InsideElement)
        {
            if (ext.State == MarkupExtensionParser.ParserStateType.InsideElement)
                forcedStart = data.Length;

            var isOnPlatform = ext.ElementName?.Trim().Equals("OnPlatform");
            var isOnFormFactor = ext.ElementName?.Trim().Equals("OnFormFactor");

            if ((isOnPlatform == true) || (isOnFormFactor == true))
            {
                // If we type a comma after a previous attribute: // {Binding Path=MyProp,
                // the parser shows that as InsideElement, though we really want that to
                // be StartAttribute for a list of completions relevant to the markup extension
                // i.e., above we'd get the completion list for {Binding} again
                bool isActuallyStartAttribute = false;
                for (int i = data.Length - 1; i >= 0; i--)
                {
                    if (data[i] == ',')
                    {
                        isActuallyStartAttribute = true;
                        break;
                    }
                    else if (data[i] == '=')
                    {
                        break;
                    }
                }

                if (isActuallyStartAttribute || ext.State == MarkupExtensionParser.ParserStateType.StartAttribute)
                {
                    if (isOnPlatform == true)
                    {
                        completions.Add(new Completion("Windows", "Windows=", "Windows", CompletionKind.Enum));
                        completions.Add(new Completion("macOS", "macOS=", "macOS", CompletionKind.Enum));
                        completions.Add(new Completion("Linux", "Linux=", "Linux", CompletionKind.Enum));
                        completions.Add(new Completion("Android", "Android=", "Android", CompletionKind.Enum));
                        completions.Add(new Completion("iOS", "iOS=", "iOS", CompletionKind.Enum));
                        completions.Add(new Completion("Browser", "Browser=", "Browser", CompletionKind.Enum));
                    }
                    else
                    {
                        completions.Add(new Completion("Desktop", "Desktop=", "Desktop", CompletionKind.Enum));
                        completions.Add(new Completion("Mobile", "Mobile=", "Mobile", CompletionKind.Enum));
                    }
                }
                else
                {
                    var prop = _helper.LookupProperty(state?.TagName, state?.AttributeName);
                    if (prop?.Type?.HasHintValues == true)
                    {
                        completions.AddRange(GetHintCompletions(prop.Type, null, currentAssemblyName));
                    }
                }

                return forcedStart ?? ext.CurrentValueStart;
            }

            completions.AddRange(_helper.FilterPropertyNames(transformedName, ext.AttributeName ?? "", attached: false, hasSetter: true)
                .Select(x => new Completion(x, x + "=", x, CompletionKind.Property)));

            var attribName = ext.AttributeName ?? "";
            var t = _helper.LookupType(transformedName);

            var ctorArgument = ext.AttributesCount == 0;
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
                else if (attribName.Contains('.'))
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
                            completions.AddRange(hints.Select(x => new Completion(x, $"{type}.{x}", x, GetCompletionKindForHintValues(mType))));
                        }

                        var props = _helper.FilterPropertyNames(type, prop, attached: false, hasSetter: false, staticGetter: true);
                        completions.AddRange(props.Select(x => new Completion(x, $"{type}.{x}", x, CompletionKind.StaticProperty)));
                    }
                }
                else
                {
                    var types = _helper.FilterTypeNames(attribName,
                        staticGettersOnly: t.SupportCtorArgument == MetadataTypeCtorArgument.Object);

                    completions.AddRange(types.Select(x => new Completion(x, x, x, CompletionKind.Class)));

                    if (property?.Type?.HasHintValues == true)
                    {
                        completions.Add(new Completion(property.Type.Name, property.Type.Name + ".", property.Type.Name, CompletionKind.Class));
                    }
                }
            }
            else
            {
                var defaultProp = t?.Properties.FirstOrDefault(p => string.IsNullOrEmpty(p.Name));
                if (defaultProp?.Type?.HasHintValues ?? false)
                {
                    completions.AddRange(GetHintCompletions(defaultProp.Type, ext.AttributeName ?? "", currentAssemblyName, fullText, state));
                }
            }
        }
        if (ext.State == MarkupExtensionParser.ParserStateType.AttributeValue
            || ext.State == MarkupExtensionParser.ParserStateType.BeforeAttributeValue)
        {
            var elementName = ext.ElementName?.Trim();
            MetadataProperty? prop;
            if (elementName?.Equals("OnPlatform") == true ||
                elementName?.Equals("OnFormFactor") == true)
            {
                prop = _helper.LookupProperty(state.TagName, state.AttributeName);
            }
            else
            {
                prop = _helper.LookupProperty(transformedName, ext.AttributeName);
            }

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
            || typedChar == '#' || typedChar == '-' || typedChar == '^' || typedChar == '{'
            || typedChar == '=' || typedChar == '[' || typedChar == '|' || typedChar == '(';
    }

    public static CompletionKind GetCompletionKindForHintValues(MetadataType type)
        => type.IsEnum ? CompletionKind.Enum : CompletionKind.StaticProperty;


    public int? ProcesssSelector(ReadOnlySpan<char> text, XmlParser state, List<Completion> completions, string? currentAssemblyName, string? fullText)
    {
        int? parsered = default;
        var parser = SelectorParser.Parse(text);
        var previusStatment = parser.PreviousStatement;
        switch (parser.Statement)
        {
            case SelectorStatement.Colon:
            case SelectorStatement.FunctionArgs:
                {
                    var fn = parser.FunctionName;
                    var tn = parser.TypeName;
                    var isEmptyTn = string.IsNullOrEmpty(tn);
                    if (previusStatment <= SelectorStatement.Middle && isEmptyTn)
                    {
                        completions.Add(new Completion(":is()", ":is(", CompletionKind.Selector | CompletionKind.Enum));
                    }
                    else if (string.IsNullOrEmpty(fn))
                    {
                        completions.Add(new Completion(":not()", ":not(", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion(":nth-child()", ":nth-child(", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion(":nth-last-child()", ":nth-last-child(", CompletionKind.Selector | CompletionKind.Enum));
                    }
                    if (isEmptyTn)
                    {
                        var pseudoClasses = _helper.FilterTypes(default)
                            .Select(kvp => kvp.Value)
                            .Where(m => m.HasPseudoClasses)
                            .SelectMany(m => m.PseudoClasses)
                            .Distinct(StringComparer.OrdinalIgnoreCase);
                        completions.AddRange(pseudoClasses.Select(v => new Completion(v, CompletionKind.Selector | CompletionKind.Enum)));
                    }
                    else
                    {
                        var typeFullName = GetFullName(parser);
                        if (_helper.LookupType(typeFullName) is MetadataType { HasPseudoClasses: true } type)
                        {
                            completions.AddRange(type.PseudoClasses.Select(v => new Completion(v, CompletionKind.Selector | CompletionKind.Enum)));
                        }
                    }
                    if (fn == "is")
                    {
                        var types = _helper.FilterTypes(default)
                               .Where(t => t.Value.IsAvaloniaObjectType)
                               .Select(t => t.Value);
                        if (types?.Any() == true)
                        {
                            parsered = text.Length - (parser.LastParsedPosition + 1);
                            completions.AddRange(types.Select(v =>
                            {
                                var name = GetXmlnsFullName(v);
                                return new Completion(name, name + ".", CompletionKind.Class | CompletionKind.TargetTypeClass);
                            }));
                        }
                    }
                    if (completions.Count > 0)
                    {
                        parsered = parser.LastParsedPosition ?? 0;
                    }
                }
                break;
            case SelectorStatement.Name:
                {
                    if (parser.IsTemplate)
                    {
                        var ton = parser.TemplateOwner;
                        if (!string.IsNullOrEmpty(ton))
                        {
                            //If it hat TemplateOwner 
                            if (_helper.FilterTypes(ton)
                                .Where(kvp => kvp.Value.TemplateParts.Any())
                                .Select(kvp => kvp.Value)
                                .FirstOrDefault() is MetadataType ownerType)
                            {
                                var parts = ownerType.TemplateParts;
                                var fullName = GetFullName(parser);
                                var partType = string.IsNullOrEmpty(fullName)
                                    ? default(MetadataType?)
                                    : _helper.FilterTypes(fullName)
                                        .Select(kvp => kvp.Value)
                                        .FirstOrDefault();
                                if (partType is not null)
                                {
                                    parts = parts
                                        .Where(p => p.Type.AssemblyQualifiedName == partType.AssemblyQualifiedName);
                                }
                                if (parts.Any())
                                {
                                    parsered = parser.LastParsedPosition ?? 0;
                                    var x = (parser.LastParsedPosition ?? 0) - parser.LastSegmentStartPosition - 1;
                                    if (string.IsNullOrEmpty(fullName) == false)
                                    {
                                        x += fullName.Length + 1;
                                    }
                                    completions.AddRange(parts!.Select(p => new Completion(p.Name, CompletionKind.Name | CompletionKind.Class, p.Type?.Name)
                                    {
                                        RecommendedCursorOffset = (p.Name.Length + (p.Type?.Name.Length > 0 ? p.Type.Name.Length + 3 : 0)),
                                        DeleteTextOffset = -x,
                                    }));
                                }
                            }
                        }
                    }
                    else if (fullText is not null)
                    {
                        var nameMatch = MetadataHelper
                            .FindElementByNameRegex
                            .Matches(fullText);
                        if (nameMatch is { Count: > 0 })
                        {
                            var filterName = nameMatch.OfType<Match>();
                            var elementName = parser.ElementName;
                            if (!string.IsNullOrEmpty(elementName))
                            {
                                filterName = filterName
                                    .Where(m => m.Groups["AttribValue"].Value.StartsWith(elementName, StringComparison.OrdinalIgnoreCase));
                            }
                            foreach (Match m in filterName)
                            {
                                if (m.Success)
                                {
                                    parsered = (parser.LastParsedPosition ?? 0);
                                    var name = m.Groups["AttribValue"].Value;
                                    completions.Add(new Completion(name, CompletionKind.Name | CompletionKind.Class));
                                }
                            }
                        }

                    }
                }
                break;
            case SelectorStatement.CanHaveType:
            case SelectorStatement.TypeName:
                {
                    var tn = parser.TypeName;
                    if (GetFullName(parser) is string typeFullName)
                    {
                        var len = typeFullName.Length;
                        if (len > 0)
                        {
                            if (typeFullName[len - 1] == ':')
                            {
                                var ns = typeFullName.Substring(0, len - 1);

                                if (_helper.Aliases?.TryGetValue(ns!, out var ans) == true
                                    && _helper.Metadata?.Namespaces.TryGetValue(ans, out var types) == true)
                                {
                                    IEnumerable<MetadataType> ft = types.Values;
                                    ft = ft
                                        .Where(t => t.IsGeneric == false)
                                        .Where(t => t.IsMarkupExtension == false)
                                        .Where(t => t.IsAvaloniaObjectType || t.HasAttachedProperties);
                                    completions.AddRange(ft.Select(v => new Completion(v.Name, $"{ns}|{v.Name}", CompletionKind.Class | CompletionKind.TargetTypeClass)));
                                    parsered = (parser.LastParsedPosition ?? 0) - (tn?.Length ?? 0);
                                }
                            }
                            else if (_helper.FilterTypes(typeFullName).Select(kvp => kvp.Value) is { } types)
                            {
                                types = types
                                        .Where(t => t.IsGeneric == false)
                                        .Where(t => t.IsMarkupExtension == false)
                                        .Where(t => t.IsAvaloniaObjectType || t.HasAttachedProperties);
                                completions.AddRange(types.Select(v =>
                                {
                                    var name = GetXmlnsFullName(v);
                                    return new Completion(name, CompletionKind.Class | CompletionKind.TargetTypeClass);
                                }));
                                parsered = (parser.LastParsedPosition ?? 0) - (tn?.Length ?? 0);
                            }
                        }
                    }
                }
                break;
            case SelectorStatement.Property:
                {
                    var typeFullName = GetFullName(parser);
                    if (_helper.LookupType(typeFullName) is MetadataType type)
                    {
                        var propertyName = parser.PropertyName;
                        var selectorElementProperties = MetadataHelper.FilterProperty(type,
                            propName: propertyName,
                            attached: default,
                            hasSetter: false
                            );
                        if (selectorElementProperties?.Any() == true)
                        {
                            parsered = (parser.LastParsedPosition ?? 0) - (propertyName?.Length ?? 0);
                            completions.AddRange(selectorElementProperties.Select(v => new Completion(v.Name, v.Name + "=", v.IsAttached ? CompletionKind.AttachedProperty : CompletionKind.Property)));
                        }
                    }
                }
                break;
            case SelectorStatement.AttachedProperty:
                {
                    var typeFullName = GetFullName(parser);
                    if (_helper.LookupType(typeFullName) is { HasAttachedProperties: true } type)
                    {
                        var propertyName = parser.PropertyName;
                        var selectorElementProperties = MetadataHelper.FilterProperty(type,
                            propName: propertyName,
                            attached: true,
                            hasSetter: false
                            );
                        if (selectorElementProperties?.Any() == true)
                        {
                            var lenPropertyName = propertyName?.Length ?? 0;
                            var lenType = lenPropertyName == 0 || typeFullName is null
                                ? 0
                                : typeFullName.Length + 1;
                            parsered = (parser.LastParsedPosition ?? 0) - lenType - lenType + 1;
                            completions.AddRange(selectorElementProperties.Select(v => new Completion(v.Name, v.Name + ")", v.IsAttached ? CompletionKind.AttachedProperty : CompletionKind.Property)));
                        }
                    }
                    else
                    {
                        var types = _helper.FilterTypes(default)
                             .Where(t => t.Value.HasAttachedProperties)
                             .Select(t => t.Value);
                        if (types?.Any() == true)
                        {
                            parsered = (parser.LastParsedPosition ?? 0) + 1;
                            completions.AddRange(types.Select(v =>
                            {
                                var name = GetXmlnsFullName(v);
                                return new Completion(name, name + ".", CompletionKind.Class);
                            }));
                        }
                    }
                }
                break;
            case SelectorStatement.Template:
                {
                    completions.Add(new("/template/", "/template/", CompletionKind.Selector | CompletionKind.Enum));
                    parsered = parser.LastParsedPosition;
                }
                break;
            case SelectorStatement.Traversal:
            case SelectorStatement.Start:
                {
                    if (!parser.IsError)
                    {
                        parsered = (parser.LastParsedPosition ?? 0);
                        // TODO: Crowling Selector operator from Attribute of the Selector
                        completions.Add(new Completion("^", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion(":", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion(">", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion(".", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion("#", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion(":is()", ":is(", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion(":not()", ":not(", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion(":nth-child()", ":nth-child(", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion(":nth-last-child()", ":nth-last-child(", CompletionKind.Selector | CompletionKind.Enum));
                        completions.Add(new Completion("/template/", "/template/", CompletionKind.Selector | CompletionKind.Enum));
                        var types = _helper.FilterTypes(default)
                            .Where(t => t.Value.IsAvaloniaObjectType || t.Value.HasAttachedProperties)
                            .Select(t => new Completion(t.Value.Name.Replace(":", "|"), CompletionKind.Class | CompletionKind.TargetTypeClass));
                        completions.AddRange(types);
                    }
                }
                break;
            case SelectorStatement.Value:
                {
                    var typeFullName = GetFullName(parser);
                    if (_helper.LookupType(typeFullName) is MetadataType type)
                    {
                        var propertyName = parser.PropertyName;
                        var prop = MetadataHelper.FilterProperty(type,
                           propName: propertyName,
                           attached: default,
                           hasSetter: false
                           ).FirstOrDefault();
                        var propType = prop?.Type;
                        if (propType?.IsNullable == true)
                        {
                            propType = propType.UnderlyingType;
                        }
                        if (propType is { HasHintValues: true } pt)
                        {
                            var kind = pt.IsEnum
                                ? CompletionKind.Enum
                                : CompletionKind.StaticProperty;
                            IEnumerable<string> values = pt.HintValues!;
                            var value = parser.Value;
                            if (!string.IsNullOrEmpty(value))
                            {
                                values = values
                                    .Where(v => v.StartsWith(value, StringComparison.OrdinalIgnoreCase));
                            }
                            completions.AddRange(values.Select(v => new Completion(v, kind)));
                            parsered = parser.LastParsedPosition - (parser.Value?.Length ?? 0);
                        }
                    }
                }
                break;
            case SelectorStatement.Function:
            case SelectorStatement.Class:
            case SelectorStatement.Middle:
            case SelectorStatement.End:
            default:
                break;
        }
        return parsered;

        string GetFullName(SelectorParser parser)
        {
            var ns = parser.Namespace;
            var typename = parser.TypeName
                ?? GetTypeFromControlTheme();
            var typeFullName = string.IsNullOrEmpty(ns)
                ? typename
                : $"{ns}:{typename}";
            return typeFullName ?? string.Empty;
        }

        string GetXmlnsFullName(MetadataType type, char namespaceSeparator = '|')
        {
            if (_helper.Metadata?.InverseNamespace.TryGetValue(type.FullName, out var ns) == true
                && !string.IsNullOrEmpty(ns))
            {
                var alias = _helper.Aliases?.FirstOrDefault(a => Equals(a.Value, ns));
                if (alias is not null && !string.IsNullOrEmpty(alias.Value.Key))
                {
                    return $"{alias.Value.Key}{namespaceSeparator}{type.Name}";
                }
            }
            return type.Name!;
        }

        string? GetTypeFromControlTheme()
        {
            if (state.GetParentTagName(1)?.Equals("ControlTheme") == true)
            {
                if (state.FindParentAttributeValue("TargetType", 1, maxLevels: 0) is string implicitSelectorTypeName)
                {
                    return implicitSelectorTypeName;
                }
            }
            return default;
        }
    }
}
