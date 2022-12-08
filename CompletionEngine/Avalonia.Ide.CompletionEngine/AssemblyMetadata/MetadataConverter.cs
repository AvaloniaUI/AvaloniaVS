using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Avalonia.Ide.CompletionEngine
{
    public static class MetadataConverter
    {
        internal static bool IsMarkupExtension(ITypeInformation type)
        {
            var def = type;

            while (def != null)
            {
                if (def.Name == "MarkupExtension")
                    return true;
                def = def.GetBaseType();
            }

            //in avalonia 0.9 there is no required base class, but convention only
            if (type.FullName.EndsWith("Extension") && type.Methods.Any(m => m.Name == "ProvideValue"))
            {
                return true;
            }

            return false;
        }

        public static MetadataType ConvertTypeInfomation(ITypeInformation type)
        {
            var mt = new MetadataType
            {
                Name = type.Name,
                FullName = type.FullName,
                IsStatic = type.IsStatic,
                IsMarkupExtension = IsMarkupExtension(type),
                IsEnum = type.IsEnum,
                HasHintValues = type.IsEnum,
                IsGeneric = type.IsGeneric,
            };
            if (mt.IsEnum)
                mt.HintValues = type.EnumValues.ToArray();
            return mt;
        }

        private class AvaresInfo
        {
            public IAssemblyInformation Assembly;
            public string ReturnTypeFullName;
            public string LocalUrl;
            public string GlobalUrl;

            public override string ToString() => GlobalUrl;
        }

        public static Metadata ConvertMetadata(IMetadataReaderSession provider)
        {
            var types = new Dictionary<string, MetadataType>();
            var typeDefs = new Dictionary<MetadataType, ITypeInformation>();
            var metadata = new Metadata();
            var resourceUrls = new List<string>();
            var avaresValues = new List<AvaresInfo>();

            var ignoredResExt = new[] { ".resources", ".rd.xml", "!AvaloniaResources" };

            bool skipRes(string res) => ignoredResExt.Any(r => res.EndsWith(r, StringComparison.OrdinalIgnoreCase));

            PreProcessTypes(types, metadata);

            foreach (var asm in provider.Assemblies)
            {
                var aliases = new Dictionary<string, string[]>();

                ProcessWellKnownAliases(asm, aliases);
                ProcessCustomAttributes(asm, aliases);

                var asmTypes = asm.Types.ToArray();

                foreach (var type in asmTypes.Where(x => !x.IsInterface && x.IsPublic))
                {
                    var mt = types[type.FullName] = ConvertTypeInfomation(type);
                    typeDefs[mt] = type;
                    metadata.AddType("clr-namespace:" + type.Namespace + ";assembly=" + asm.Name, mt);
                    string[] nsAliases = null;
                    string usingNamespace = $"using:{type.Namespace}";
                    if (!aliases.TryGetValue(type.Namespace, out nsAliases))
                    {
                        nsAliases = new string[] { usingNamespace };
                        aliases[type.Namespace] = nsAliases;
                    }
                    else if (!nsAliases.Contains(usingNamespace))
                    {
                        aliases[type.Namespace] = nsAliases.Union(new string[] { usingNamespace }).ToArray();
                    }

                    foreach (var alias in nsAliases) metadata.AddType(alias, mt);
                }

                ProcessAvaloniaResources(asm, asmTypes, avaresValues);

                resourceUrls.AddRange(asm.ManifestResourceNames.Where(r => !skipRes(r)).Select(r => $"resm:{r}?assembly={asm.Name}"));
            }

            foreach (var type in types.Values)
            {
                ITypeInformation typeDef;
                typeDefs.TryGetValue(type, out typeDef);

                var ctors = typeDef?.Methods
                    .Where(m => m.IsPublic && !m.IsStatic && m.Name == ".ctor" && m.Parameters.Count == 1);

                if (typeDef?.IsEnum ?? false)
                {
                    foreach (var value in typeDef.EnumValues)
                    {
                        var p = new MetadataProperty(value, type, type, false, true, true, false);

                        type.Properties.Add(p);
                    }
                }

                int level = 0;
                while (typeDef != null)
                {
                    var currentType = types.GetValueOrDefault(typeDef.FullName);
                    foreach (var prop in typeDef.Properties)
                    {
                        if (!prop.HasPublicGetter && !prop.HasPublicSetter)
                            continue;

                        var p = new MetadataProperty(prop.Name, types.GetValueOrDefault(prop.TypeFullName),
                            currentType, false, prop.IsStatic, prop.HasPublicGetter,
                            prop.HasPublicSetter);

                        type.Properties.Add(p);
                    }

                    foreach (var eventDef in typeDef.Events)
                    {
                        var e = new MetadataEvent(eventDef.Name, types.GetValueOrDefault(eventDef.TypeFullName),
                            types.GetValueOrDefault(typeDef.FullName), false);

                        type.Events.Add(e);
                    }

                    //check for attached properties only on top level
                    if (level == 0)
                    {
                        foreach (var methodDef in typeDef.Methods)
                        {
                            if (methodDef.Name.StartsWith("Set", StringComparison.OrdinalIgnoreCase) && methodDef.IsStatic && methodDef.IsPublic
                                && methodDef.Parameters.Count == 2)
                            {
                                var name = methodDef.Name.Substring(3);
                                type.Properties.Add(new MetadataProperty(name,
                                    types.GetValueOrDefault(methodDef.Parameters[1].TypeFullName),
                                    types.GetValueOrDefault(typeDef.FullName),
                                    true, false, true, true));
                            }
                        }

                        foreach (var fieldDef in typeDef.Fields)
                        {
                            if (fieldDef.IsStatic && fieldDef.IsPublic)
                            {
                                if ((fieldDef.IsRoutedEvent || fieldDef.Name.EndsWith("Event", StringComparison.OrdinalIgnoreCase)))
                                {
                                    var name = fieldDef.Name;
                                    if (fieldDef.Name.EndsWith("Event", StringComparison.OrdinalIgnoreCase))
                                    {
                                        name = name.Substring(0, name.Length - "Event".Length);
                                    }

                                    type.Events.Add(new MetadataEvent(name,
                                        types.GetValueOrDefault(fieldDef.ReturnTypeFullName),
                                        types.GetValueOrDefault(typeDef.FullName),
                                        true));
                                }
                                else if(type.IsStatic)
                                {
                                    type.Properties.Add(new MetadataProperty(fieldDef.Name, null, type, false, true, true, false));
                                }
                            }
                        }
                    }

                    if (typeDef.FullName == "Avalonia.AvaloniaObject")
                    {
                        type.IsAvaloniaObjectType = true;
                    }

                    typeDef = typeDef.GetBaseType();
                    level++;
                }

                type.HasAttachedProperties = type.Properties.Any(p => p.IsAttached);
                type.HasAttachedEvents = type.Events.Any(e => e.IsAttached);
                type.HasStaticGetProperties = type.Properties.Any(p => p.IsStatic && p.HasGetter);
                type.HasSetProperties = type.Properties.Any(p => !p.IsStatic && p.HasSetter);

                if (ctors?.Any() == true)
                {
                    bool supportType = ctors.Any(m => m.Parameters[0].TypeFullName == "System.Type");
                    bool supportObject = ctors.Any(m => m.Parameters[0].TypeFullName == "System.Object" ||
                                                        m.Parameters[0].TypeFullName == "System.String");

                    if (types.TryGetValue(ctors.First().Parameters[0].TypeFullName, out MetadataType parType)
                            && parType.HasHintValues)
                    {
                        type.SupportCtorArgument = MetadataTypeCtorArgument.HintValues;
                        type.HasHintValues = true;
                        type.HintValues = parType.HintValues;
                    }
                    else if (supportType && supportObject)
                        type.SupportCtorArgument = MetadataTypeCtorArgument.TypeAndObject;
                    else if (supportType)
                        type.SupportCtorArgument = MetadataTypeCtorArgument.Type;
                    else if (supportObject)
                        type.SupportCtorArgument = MetadataTypeCtorArgument.Object;
                }
            }

            PostProcessTypes(types, metadata, resourceUrls, avaresValues);

            return metadata;
        }

        private static void ProcessAvaloniaResources(IAssemblyInformation asm, ITypeInformation[] asmTypes, List<AvaresInfo> avaresValues)
        {
            const string avaresToken = "Build:"; //or "Populate:" should work both ways

            void registeravares(string localUrl, string returnTypeFullName = "")
            {
                var globalUrl = $"avares://{asm.Name}{localUrl}";

                if (!avaresValues.Any(v => v.GlobalUrl == globalUrl))
                {
                    var avres = new AvaresInfo
                    {
                        Assembly = asm,
                        LocalUrl = localUrl,
                        GlobalUrl = globalUrl,
                        ReturnTypeFullName = returnTypeFullName
                    };

                    avaresValues.Add(avres);
                }
            }

            var resType = asmTypes.FirstOrDefault(t => t.FullName == "CompiledAvaloniaXaml.!AvaloniaResources");
            if (resType != null)
            {
                foreach (var res in resType.Methods.Where(m => m.Name.StartsWith(avaresToken)))
                {
                    registeravares(res.Name.Replace(avaresToken, ""), res.ReturnTypeFullName ?? "");
                }
            }

            //try add avares Embedded resources like image,stream and x:Class
            if (asm.ManifestResourceNames.Contains("!AvaloniaResources"))
            {
                try
                {
                    using (var avaresStream = asm.GetManifestResourceStream("!AvaloniaResources"))
                    using (var r = new BinaryReader(avaresStream))
                    {
                        var ms = new MemoryStream(r.ReadBytes(r.ReadInt32()));
                        var br = new BinaryReader(ms);

                        int version = br.ReadInt32();
                        if (version == 1)
                        {
                            var assetDoc = XDocument.Load(ms);
                            var ns = assetDoc.Root.GetDefaultNamespace();
                            var avaResEntries = assetDoc.Root.Element(ns.GetName("Entries")).Elements(ns.GetName("AvaloniaResourcesIndexEntry"))
                                .Select(entry => new
                                {
                                    Path = entry.Element(ns.GetName("Path")).Value,
                                    Offset = int.Parse(entry.Element(ns.GetName("Offset")).Value),
                                    Size = int.Parse(entry.Element(ns.GetName("Size")).Value)
                                }).ToArray();

                            var xClassEntries = avaResEntries.FirstOrDefault(v => v.Path == "/!AvaloniaResourceXamlInfo");

                            //get information about x:Class resources
                            if (xClassEntries != null && xClassEntries.Size > 0)
                            {
                                try
                                {
                                    avaresStream.Seek(xClassEntries.Offset, SeekOrigin.Current);
                                    var xClassDoc = XDocument.Load(new MemoryStream(r.ReadBytes(xClassEntries.Size)));
                                    var xClassMappingNode = xClassDoc.Root.Element(xClassDoc.Root.GetDefaultNamespace().GetName("ClassToResourcePathIndex"));
                                    if (xClassMappingNode != null)
                                    {
                                        const string arraysNs = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
                                        var keyvalueofss = XName.Get("KeyValueOfstringstring", arraysNs);
                                        var keyName = XName.Get("Key", arraysNs);
                                        var valueName = XName.Get("Value", arraysNs);

                                        var xClassMappings = xClassMappingNode.Elements(keyvalueofss)
                                                        .Where(e => e.Elements(keyName).Any() && e.Elements(valueName).Any())
                                                        .Select(e => new
                                                        {
                                                            Type = e.Element(keyName).Value,
                                                            Path = e.Element(valueName).Value,
                                                        }).ToArray();

                                        foreach (var xcm in xClassMappings)
                                        {
                                            var resultType = asmTypes.FirstOrDefault(t => t.FullName == xcm.Type);
                                            //if we need another check
                                            //if (resultType?.Methods?.Any(m => m.Name == "!XamlIlPopulate") ?? false)
                                            if (resultType != null)
                                            {
                                                //we set here base class like Style, Styles, UserControl so we can manage
                                                //resources in a common way later
                                                registeravares(xcm.Path, resultType.GetBaseType()?.FullName ?? "");
                                            }
                                        }
                                    }
                                }
                                catch (Exception xClassEx)
                                {
                                    Console.WriteLine($"Failed fetch avalonia x:class resources in {asm.Name}, {xClassEx.Message}");
                                }
                            }

                            //add other img/stream resources
                            foreach (var entry in avaResEntries.Where(v => !v.Path.StartsWith("/!")))
                            {
                                registeravares(entry.Path);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed fetch avalonia resources in {asm.Name}, {ex.Message}");
                }
            }
        }

        private static void ProcessCustomAttributes(IAssemblyInformation asm, Dictionary<string, string[]> aliases)
        {
            foreach (
                var attr in
                asm.CustomAttributes.Where(a => a.TypeFullName == "Avalonia.Metadata.XmlnsDefinitionAttribute" ||
                                                a.TypeFullName == "Portable.Xaml.Markup.XmlnsDefinitionAttribute"))
            {
                var ns = attr.ConstructorArguments[1].Value.ToString();
                var current = new[] { attr.ConstructorArguments[0].Value.ToString() };
                string[] allns = null;

                if (aliases.TryGetValue(ns, out allns))
                    allns = allns.Union(current).Distinct().ToArray();

                aliases[ns] = allns ?? current;
            }
        }

        private static void ProcessWellKnownAliases(IAssemblyInformation asm, Dictionary<string, string[]> aliases)
        {
            //look like we don't have xmlns for avalonia.layout TODO: add it in avalonia
            //may be don 't remove it for avalonia 0.7 or below for support completion for layout enums etc.
            aliases["Avalonia.Layout"] = new[] { "https://github.com/avaloniaui" };
        }

        private static void PreProcessTypes(Dictionary<string, MetadataType> types, Metadata metadata)
        {
            MetadataType xDataType, xCompiledBindings, boolType, typeType;
            var toAdd = new List<MetadataType>
            {
                (boolType = new MetadataType()
                {
                    Name = typeof(bool).FullName,
                    HasHintValues = true,
                    HintValues = new[] { "True", "False" }
                }),
                new MetadataType(){ Name = typeof(System.Uri).FullName },
                (typeType = new MetadataType(){ Name = typeof(System.Type).FullName }),
                new MetadataType(){ Name = "Avalonia.Media.IBrush" },
                new MetadataType(){ Name = "Avalonia.Media.Imaging.IBitmap" },
                new MetadataType(){ Name = "Avalonia.Media.IImage" },
            };

            foreach (var t in toAdd)
                types.Add(t.Name, t);

            var portableXamlExtTypes = new[]
            {
                new MetadataType()
                {
                    Name = "StaticExtension",
                    SupportCtorArgument = MetadataTypeCtorArgument.Object,
                    HasSetProperties = true,
                    IsMarkupExtension = true,
                },
                new MetadataType()
                {
                    Name = "TypeExtension",
                    SupportCtorArgument = MetadataTypeCtorArgument.TypeAndObject,
                    HasSetProperties = true,
                    IsMarkupExtension = true,
                },
                new MetadataType()
                {
                    Name = "NullExtension",
                    HasSetProperties = true,
                    IsMarkupExtension = true,
                },
                new MetadataType()
                {
                    Name = "Class",
                    IsXamlDirective = true
                },
                new MetadataType()
                {
                    Name = "Name",
                    IsXamlDirective = true
                },
                new MetadataType()
                {
                    Name = "Key",
                    IsXamlDirective = true
                },
                xDataType = new MetadataType()
                {
                    Name = "DataType",
                    IsXamlDirective = true,
                    Properties = { new MetadataProperty("", typeType,null, false, false, false, true)},
                },
                xCompiledBindings = new MetadataType()
                {
                    Name = "CompileBindings",
                    IsXamlDirective = true,
                    Properties = { new MetadataProperty("", boolType,null, false, false, false, true)},
                },
            };

            //as in avalonia 0.9 Portablexaml is missing we need to hardcode some extensions
            foreach (var t in portableXamlExtTypes)
            {
                metadata.AddType(Utils.Xaml2006Namespace, t);
            }

            types.Add(xDataType.Name, xDataType);
            types.Add(xCompiledBindings.Name, xCompiledBindings);

            metadata.AddType("", new MetadataType() { Name = "xmlns", IsXamlDirective = true });
        }

        private static void PostProcessTypes(Dictionary<string, MetadataType> types, Metadata metadata, IEnumerable<string> resourceUrls, List<AvaresInfo> avaResValues)
        {
            bool rhasext(string resource, string ext) => resource.StartsWith("resm:") ? resource.Contains(ext + "?assembly=") : resource.EndsWith(ext);

            var allresourceUrls = avaResValues.Select(v => v.GlobalUrl).Concat(resourceUrls).ToArray();

            var resType = new MetadataType()
            {
                Name = "avares://,resm:",
                IsStatic = true,
                HasHintValues = true,
                HintValues = allresourceUrls
            };

            types.Add(resType.Name, resType);

            var xamlResType = new MetadataType()
            {
                Name = "avares://*.xaml,resm:*.xaml",
                HasHintValues = true,
                HintValues = resType.HintValues.Where(r => rhasext(r, ".xaml") || rhasext(r, ".paml") || rhasext(r, ".axaml")).ToArray()
            };

            var styleResType = new MetadataType()
            {
                Name = "Style avares://*.xaml,resm:*.xaml",
                HasHintValues = true,
                HintValues = avaResValues.Where(v => v.ReturnTypeFullName.StartsWith("Avalonia.Styling.Style"))
                                        .Select(v => v.GlobalUrl)
                                        .Concat(resourceUrls.Where(r => rhasext(r, ".xaml") || rhasext(r, ".paml") || rhasext(r, ".axaml")))
                                        .ToArray()
            };

            types.Add(styleResType.Name, styleResType);

            IEnumerable<string> filterLocalRes(MetadataType type, string currentAssemblyName)
            {
                var localResPrefix = $"avares://{currentAssemblyName}";
                var resmSuffix = $"?assembly={currentAssemblyName}";

                foreach (var hint in type.HintValues ?? Array.Empty<string>())
                {
                    if (hint.StartsWith("avares://"))
                    {
                        if (hint.StartsWith(localResPrefix))
                        {
                            yield return hint.Substring(localResPrefix.Length);
                        }
                    }
                    else if (hint.StartsWith("resm:"))
                    {
                        if (hint.EndsWith(resmSuffix))
                        {
                            yield return hint.Substring(0, hint.Length - resmSuffix.Length);
                        }
                    }
                }
            }

            resType.XamlContextHintValuesFunc = (a, t, p) => filterLocalRes(xamlResType, a);
            xamlResType.XamlContextHintValuesFunc = (a, t, p) => filterLocalRes(xamlResType, a);
            styleResType.XamlContextHintValuesFunc = (a, t, p) => filterLocalRes(styleResType, a);

            types.Add(xamlResType.Name, xamlResType);

            var allProps = new Dictionary<string, MetadataProperty>();

            foreach (var type in types.Where(t => t.Value.IsAvaloniaObjectType))
            {
                foreach (var v in type.Value.Properties.Where(p => p.HasSetter && p.HasGetter))
                {
                    allProps[v.Name] = v;
                }
            }

            string[] allAvaloniaProps = allProps.Keys.ToArray();

            if (!types.TryGetValue("Avalonia.Markup.Xaml.MarkupExtensions.BindingExtension", out MetadataType bindingExtType))
            {
                if (types.TryGetValue("Avalonia.Data.Binding", out MetadataType origBindingType))
                {
                    //avalonia 0.10 has implicit binding extension
                    bindingExtType = origBindingType.CloneAs("BindingExtension",
                        "Avalonia.Markup.Xaml.MarkupExtensions.BindingExtension");
                    bindingExtType.IsMarkupExtension = true;

                    types.Add(bindingExtType.FullName, bindingExtType);
                    metadata.AddType(Utils.AvaloniaNamespace, bindingExtType);
                }
            }

            types.TryGetValue("Avalonia.Controls.Control", out MetadataType controlType);
            types.TryGetValue(typeof(Type).FullName, out MetadataType typeType);

            var dataContextType = new MetadataType()
            {
                Name = "{BindingPath}",
                FullName = "{BindingPath}",
                HasHintValues = true,
                HintValues = new[] { "$parent", "$parent[", "$self" },
            };

            //bindings related hints
            if (types.TryGetValue("Avalonia.Markup.Xaml.MarkupExtensions.BindingExtension", out MetadataType bindingType))
            {
                bindingType.SupportCtorArgument = MetadataTypeCtorArgument.None;
                var pathProp = bindingType.Properties.FirstOrDefault(p => p.Name == "Path");
                if (pathProp != null) pathProp.Type = dataContextType;

                bindingType.Properties.Add(new MetadataProperty("", dataContextType, bindingType, false, false, true, true));
            }

            if (types.TryGetValue("Avalonia.Data.TemplateBinding", out MetadataType templBinding))
            {
                var tbext = new MetadataType()
                {
                    Name = "TemplateBindingExtension",
                    IsMarkupExtension = true,
                    Properties = templBinding.Properties,
                    SupportCtorArgument = MetadataTypeCtorArgument.HintValues,
                    HasHintValues = allAvaloniaProps?.Any() ?? false,
                    HintValues = allAvaloniaProps
                };

                types["TemplateBindingExtension"] = tbext;
                metadata.AddType(Utils.AvaloniaNamespace, tbext);
            }

            if (types.TryGetValue("Portable.Xaml.Markup.TypeExtension", out MetadataType typeExtension))
            {
                typeExtension.SupportCtorArgument = MetadataTypeCtorArgument.Type;
            }

            //TODO: may be make it to load from assembly resources
            string[] commonResKeys = new string[] {
//common brushes
"ThemeBackgroundBrush","ThemeBorderLowBrush","ThemeBorderMidBrush","ThemeBorderHighBrush",
"ThemeControlLowBrush","ThemeControlMidBrush","ThemeControlHighBrush",
"ThemeControlHighlightLowBrush","ThemeControlHighlightMidBrush","ThemeControlHighlightHighBrush",
"ThemeForegroundBrush","ThemeForegroundLowBrush","HighlightBrush",
"ThemeAccentBrush","ThemeAccentBrush2","ThemeAccentBrush3","ThemeAccentBrush4",
"ErrorBrush","ErrorLowBrush",
//some other usefull
"ThemeBorderThickness", "ThemeDisabledOpacity",
"FontSizeSmall","FontSizeNormal","FontSizeLarge"
                };

            if (types.TryGetValue("Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension", out MetadataType dynRes))
            {
                dynRes.SupportCtorArgument = MetadataTypeCtorArgument.HintValues;
                dynRes.HasHintValues = true;
                dynRes.HintValues = commonResKeys;
            }

            if (types.TryGetValue("Avalonia.Markup.Xaml.MarkupExtensions.StaticResourceExtension", out MetadataType stRes))
            {
                stRes.SupportCtorArgument = MetadataTypeCtorArgument.HintValues;
                stRes.HasHintValues = true;
                stRes.HintValues = commonResKeys;
            }

            //brushes
            if (types.TryGetValue("Avalonia.Media.IBrush", out MetadataType brushType) &&
                types.TryGetValue("Avalonia.Media.Brushes", out MetadataType brushes))
            {
                brushType.HasHintValues = true;
                brushType.HintValues = brushes.Properties.Where(p => p.IsStatic && p.HasGetter).Select(p => p.Name).ToArray();
            }

            if (types.TryGetValue("Avalonia.Styling.Selector", out MetadataType styleSelector))
            {
                styleSelector.HasHintValues = true;
                styleSelector.IsCompositeValue = true;

                List<string> hints = new List<string>();

                //some reserved words
                hints.AddRange(new[] { "/template/", ":is()", ">", "#", "." });

                //some pseudo classes
                hints.AddRange(new[]
                {
                    ":pointerover", ":pressed", ":disabled", ":focus",
                    ":selected", ":vertical", ":horizontal",
                    ":checked", ":unchecked", ":indeterminate"
                });

                hints.AddRange(types.Where(t => t.Value.IsAvaloniaObjectType).Select(t => t.Value.Name.Replace(":", "|")));

                styleSelector.HintValues = hints.ToArray();
            }

            string[] bitmaptypes = new[] { ".jpg", ".bmp", ".png", ".ico" };

            bool isbitmaptype(string resource) => bitmaptypes.Any(ext => rhasext(resource, ext));

            if (types.TryGetValue("Avalonia.Media.Imaging.IBitmap", out MetadataType ibitmapType))
            {
                ibitmapType.HasHintValues = true;
                ibitmapType.HintValues = allresourceUrls.Where(r => isbitmaptype(r)).ToArray();
                ibitmapType.XamlContextHintValuesFunc = (a, t, p) => filterLocalRes(ibitmapType, a);
            }

            if (types.TryGetValue("Avalonia.Media.IImage", out MetadataType iImageType))
            {
                iImageType.HasHintValues = true;
                iImageType.HintValues = allresourceUrls.Where(r => isbitmaptype(r)).ToArray();
                iImageType.XamlContextHintValuesFunc = (a, t, p) => filterLocalRes(ibitmapType, a);
            }

            if (types.TryGetValue("Avalonia.Controls.WindowIcon", out MetadataType winIcon))
            {
                winIcon.HasHintValues = true;
                winIcon.HintValues = allresourceUrls.Where(r => rhasext(r, ".ico")).ToArray();
                winIcon.XamlContextHintValuesFunc = (a, t, p) => filterLocalRes(winIcon, a);
            }

            if (types.TryGetValue("Avalonia.Markup.Xaml.Styling.StyleInclude", out MetadataType styleIncludeType))
            {
                var source = styleIncludeType.Properties.FirstOrDefault(p => p.Name == "Source");

                if (source != null)
                    source.Type = styleResType;
            }

            if (types.TryGetValue("Avalonia.Markup.Xaml.Styling.StyleIncludeExtension", out MetadataType styleIncludeExtType))
            {
                var source = styleIncludeExtType.Properties.FirstOrDefault(p => p.Name == "Source");

                if (source != null)
                    source.Type = xamlResType;
            }

            if (types.TryGetValue(typeof(Uri).FullName, out MetadataType uriType))
            {
                uriType.HasHintValues = true;
                uriType.HintValues = allresourceUrls.ToArray();
                uriType.XamlContextHintValuesFunc = (a, t, p) => filterLocalRes(uriType, a);
            }

            if (typeType != null)
            {
                var typeArguments = new MetadataType()
                {
                    Name = "TypeArguments",
                    IsXamlDirective = true,
                    IsValidForXamlContextFunc = (a, t, p) => t?.IsGeneric == true,
                    Properties = { new MetadataProperty("", typeType, null, false, false, false, true) }
                };

                metadata.AddType(Utils.Xaml2006Namespace, typeArguments);
            }
        }
    }
}