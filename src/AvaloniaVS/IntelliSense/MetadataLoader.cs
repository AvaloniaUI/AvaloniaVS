using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;

namespace AvaloniaVS.IntelliSense
{
    public class Metadata
    {
        public Dictionary<string, Dictionary<string, MetadataType>> Namespaces { get; } = new Dictionary<string, Dictionary<string, MetadataType>>();

        public void AddType(string ns, MetadataType type) => Namespaces.GetOrCreate(ns)[type.Name] = type;
    }

    public class MetadataType
    {
        public bool IsMarkupExtension { get; set; }
        public bool IsStatic { get; set; }
        public string Name { get; set; }
        public List<MetadataProperty> Properties { get; set; } = new List<MetadataProperty>();
        public bool HasAttachedProperties { get; set; }
    }
    
    public class MetadataProperty
    {
        public string Name { get; set; }
        public bool IsAttached { get; set; }
        public MetadataPropertyType Type { get; set; }
        public string[] EnumValues { get; set; }
    }

    public class CustomEnumTypeDef : TypeDefUser
    {
        public CustomEnumTypeDef(UTF8String name, string[] values) : base(name)
        {
            Values = values;
        }

        public string[] Values { get; }
    }

    public enum MetadataPropertyType
    {
        String,
        Enum
    }
    

    public static class MetadataLoader
    {
        static bool IsMarkupExtensions(ITypeDefOrRef def)
        {
            while (def != null)
            {
                if (def.Namespace == "OmniXaml" && def.Name.String == "MarkupExtension")
                    return true;
                def = def.GetBaseType();
            }
            return false;
        }


        static void ResolvePropertyType(TypeDef type, MetadataProperty info)
        {
            if (type is CustomEnumTypeDef)
            {
                var ct = type as CustomEnumTypeDef;
                info.Type = MetadataPropertyType.Enum;
                info.EnumValues = ct.Values;
            }
            else if (type.IsEnum)
            {
                info.Type = MetadataPropertyType.Enum;
                info.EnumValues = type.Fields.Where(f => f.IsStatic).Select(f => f.Name.String).ToArray();
            }
        }

        public static Metadata LoadMetadata(string target)
        {
            var types = new Dictionary<string, MetadataType>();
            var typeDefs = new Dictionary<MetadataType, TypeDef>();
            var metadata = new Metadata();

            // add easilly the bool type and other types in the future like Brushes posiibly
            var td = new CustomEnumTypeDef(typeof(bool).FullName, new[] { "True", "False" });
            types.Add(typeof(bool).FullName, new MetadataType() { Name = typeof(bool).FullName });
            typeDefs.Add(types[typeof(bool).FullName], td);

            foreach (var asm in LoadAssemblies(target))
            {
                var aliases = new Dictionary<string, string>();
                foreach (var attr in asm.CustomAttributes.FindAll("Avalonia.Metadata.XmlnsDefinitionAttribute"))
                    aliases[attr.ConstructorArguments[1].Value.ToString()] =
                        attr.ConstructorArguments[0].Value.ToString();

                foreach (var type in asm.Modules.SelectMany(m => m.GetTypes()).Where(x => !x.IsInterface && x.IsPublic))
                {
                    var mt = types[type.FullName] = new MetadataType
                    {
                        Name = type.Name,
                        IsStatic = type.IsSealed && type.IsAbstract,
                        IsMarkupExtension = IsMarkupExtensions(type)
                    };
                    typeDefs[mt] = type;
                    metadata.AddType("clr-namespace:" + type.Namespace + ";assembly=" + asm.Name, mt);
                    string alias = null;
                    if (aliases.TryGetValue(type.Namespace, out alias))
                        metadata.AddType(alias, mt);
                }
            }

            foreach (var type in types.Values)
            {
                var typeDef = typeDefs[type];
                while (typeDef != null)
                {
                    foreach (var prop in typeDef.Properties)
                    {
                        var setMethod = prop.SetMethod;
                        if (setMethod == null || setMethod.IsStatic || !setMethod.IsPublic)
                            continue;

                        var p = new MetadataProperty { Name = prop.Name };

                        if (setMethod.Parameters.Count == 2)
                        {
                            //1 param this, 2 param prop value
                            var mt = types.GetValueOrDefault(setMethod.Parameters[1].Type.FullName);

                            if (mt != null)
                                ResolvePropertyType(typeDefs[mt], p);
                        }

                        type.Properties.Add(p);
                    }
                    foreach (var methodDef in typeDef.Methods)
                    {
                        if (methodDef.Name.StartsWith("Set") && methodDef.IsStatic && methodDef.IsPublic
                            && methodDef.Parameters.Count == 2)
                        {
                            var p = new MetadataProperty() { Name = methodDef.Name.Substring(3), IsAttached = true };
                            var mt = types.GetValueOrDefault(methodDef.Parameters[1].Type.FullName);
                            if (mt != null)
                                ResolvePropertyType(typeDefs[mt], p);
                            type.Properties.Add(p);
                        }
                    }
                    typeDef = typeDef.GetBaseType().ResolveTypeDef();
                }
                type.HasAttachedProperties = type.Properties.Any(p => p.IsAttached);
            }

            return metadata;
        }


        static List<AssemblyDef> LoadAssemblies(string target)
        {
            AssemblyResolver asmResolver = new AssemblyResolver();
            ModuleContext modCtx = new ModuleContext(asmResolver);
            asmResolver.DefaultModuleContext = modCtx;
            asmResolver.EnableTypeDefCache = true;

            var directory = Path.GetFullPath(Path.GetDirectoryName(target));
            asmResolver.PreSearchPaths.Add(directory);

            List<AssemblyDef> assemblies = new List<AssemblyDef>();
            
            foreach (var asm in Directory.GetFiles(directory, "*.*")
                .Where(f=>Path.GetExtension(f.ToLower()) == ".exe" || Path.GetExtension(f.ToLower())==".dll"))
            {
                try
                {
                    var def = AssemblyDef.Load(asm);
                    def.Modules[0].Context = modCtx;
                    asmResolver.AddToCache(def);
                    assemblies.Add(def);
                }
                catch
                {
                    //Ignore
                }
            }

            return assemblies;
        }
    }
}
