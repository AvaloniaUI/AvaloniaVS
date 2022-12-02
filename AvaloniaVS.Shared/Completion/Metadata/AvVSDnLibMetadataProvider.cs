using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using dnlib.DotNet;

namespace AvaloniaVS.Shared.Completion.Metadata
{
    public class AvVSDnLibMetadataProvider : IMetadataProvider
    {
        public IMetadataReaderSession GetMetadata(IEnumerable<string> paths)
        {
            return new AvVSDnlibMetadataProviderSession(paths.ToArray());
        }
    }

    public class AvVSDnlibMetadataProviderSession : IMetadataReaderSession
    {
        public IEnumerable<IAssemblyInformation> Assemblies { get; }

        public AvVSDnlibMetadataProviderSession(string[] directoryPath)
        {
            Assemblies = LoadAssemblies(directoryPath).Select(a => new AssemblyWrapper(a)).ToList();
        }

        static List<AssemblyDef> LoadAssemblies(string[] lst)
        {
            AssemblyResolver asmResolver = new AssemblyResolver();
            ModuleContext modCtx = new ModuleContext(asmResolver);
            asmResolver.DefaultModuleContext = modCtx;
            asmResolver.EnableTypeDefCache = true;

            foreach (var path in lst)
                asmResolver.PreSearchPaths.Add(path);

            List<AssemblyDef> assemblies = new List<AssemblyDef>();

            foreach (var asm in lst)
            {
                try
                {
                    var def = AssemblyDef.Load(File.ReadAllBytes(asm));
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

        public void Dispose()
        {
            //no-op
        }
    }

    class AssemblyWrapper : IAvVSAssemblyInformation
    {
        private readonly AssemblyDef _asm;

        public AssemblyWrapper(AssemblyDef asm)
        {
            _asm = asm;
        }

        public string Name => _asm.Name;

        public IEnumerable<ITypeInformation> Types
            => _asm.Modules.SelectMany(m => m.Types).Select(TypeWrapper.FromDef);

        public IEnumerable<ICustomAttributeInformation> CustomAttributes
            => _asm.CustomAttributes.Select(a => new CustomAttributeWrapper(a));

        public IEnumerable<string> ManifestResourceNames
            => _asm.ManifestModule.Resources.Select(r => r.Name.ToString());

        public Stream GetManifestResourceStream(string name)
            => _asm.ManifestModule.Resources.FindEmbeddedResource(name).CreateReader().AsStream();

        public override string ToString() => Name;
    }

    class TypeWrapper : IAvVSTypeInformation
    {
        private readonly TypeDef _type;

        public static TypeWrapper FromDef(TypeDef def) => def == null ? null : new TypeWrapper(def);

        TypeWrapper(TypeDef type)
        {
            if (type == null)
                throw new ArgumentNullException();
            _type = type;
        }

        public string FullName => _type.FullName;

        public string Name => _type.Name;

        public string Namespace => _type.Namespace;

        public ITypeInformation GetBaseType() => FromDef(_type.GetBaseType().ResolveTypeDef());


        public IEnumerable<IEventInformation> Events => _type.Events.Select(e => new EventWrapper(e));

        public IEnumerable<IMethodInformation> Methods => _type.Methods.Select(m => new MethodWrapper(m));

        public IEnumerable<IFieldInformation> Fields => _type.Fields.Select(f => new FieldWrapper(f));

        public IEnumerable<IPropertyInformation> Properties => _type.Properties
            //Filter indexer properties
            .Where(p =>
                (p.GetMethod?.IsPublicOrInternal() == true && p.GetMethod.Parameters.Count == (p.GetMethod.IsStatic ? 0 : 1))
                || (p.SetMethod?.IsPublicOrInternal() == true && p.SetMethod.Parameters.Count == (p.SetMethod.IsStatic ? 1 : 2)))
            // Filter property overrides
            .Where(p => !p.Name.Contains("."))
            .Select(p => new PropertyWrapper(p));

        public bool IsEnum => _type.IsEnum;

        public bool IsStatic => _type.IsAbstract && _type.IsSealed;

        public bool IsInterface => _type.IsInterface;

        public bool IsPublic => _type.IsPublic;

        public bool IsGeneric => _type.HasGenericParameters;

        public IEnumerable<string> EnumValues
        {
            get
            {
                return _type.Fields.Where(f => f.IsStatic).Select(f => f.Name.String).ToArray();
            }
        }

        public override string ToString() => Name;

        public IEnumerable<ITypeInformation> NestedTypes =>
            _type.HasNestedTypes ? _type.NestedTypes.Select(t => new TypeWrapper(t)) : Array.Empty<TypeWrapper>();
    }

    class CustomAttributeWrapper : ICustomAttributeInformation
    {
        private Lazy<IList<IAttributeConstructorArgumentInformation>> _args;
        public CustomAttributeWrapper(CustomAttribute attr)
        {
            TypeFullName = attr.TypeFullName;
            _args = new Lazy<IList<IAttributeConstructorArgumentInformation>>(() =>
                attr.ConstructorArguments.Select(
                    ca => (IAttributeConstructorArgumentInformation)
                        new ConstructorArgumentWrapper(ca)).ToList());
        }

        public string TypeFullName { get; }

        public IList<IAttributeConstructorArgumentInformation> ConstructorArguments => _args.Value;
    }

    class ConstructorArgumentWrapper : IAttributeConstructorArgumentInformation
    {
        public ConstructorArgumentWrapper(CAArgument ca)
        {
            Value = ca.Value;
        }

        public object Value { get; }
    }

    class PropertyWrapper : IAvVSPropertyInformation
    {
        public PropertyWrapper(PropertyDef prop)
        {
            Name = prop.Name;
            var setMethod = prop.SetMethod;
            var getMethod = prop.GetMethod;

            IsStatic = setMethod?.IsStatic ?? getMethod?.IsStatic ?? false;

            if (setMethod?.IsPublicOrInternal() == true)
            {
                HasPublicSetter = true;
                TypeFullName = setMethod.Parameters[setMethod.IsStatic ? 0 : 1].Type.FullName;
            }

            if (getMethod?.IsPublicOrInternal() == true)
            {
                HasPublicGetter = true;
                if (TypeFullName == null)
                    TypeFullName = getMethod.ReturnType.FullName;
            }
        }

        public bool IsStatic { get; }

        public bool HasPublicSetter { get; }

        public bool HasPublicGetter { get; }

        public string TypeFullName { get; }

        public string Name { get; }

        public override string ToString() => Name;
    }

    class FieldWrapper : IAvVSFieldInformation
    {
        public FieldWrapper(FieldDef f)
        {
            IsStatic = f.IsStatic;
            IsPublic = f.IsPublic || f.IsAssembly;
            Name = f.Name;
            ReturnTypeFullName = f.FieldType.FullName;

            bool isRoutedEvent = false;
            ITypeDefOrRef t = f.FieldType.ToTypeDefOrRef();
            while (t != null)
            {
                if (t.Name == "RoutedEvent" && t.Namespace == "Avalonia.Interactivity")
                {
                    isRoutedEvent = true;
                    break;
                }
                t = t.GetBaseType();
            }

            IsRoutedEvent = isRoutedEvent;
        }

        public bool IsRoutedEvent { get; }

        public bool IsStatic { get; }

        public bool IsPublic { get; }

        public string Name { get; }

        public string ReturnTypeFullName { get; }
    }

    class EventWrapper : IAvVSEventInformation
    {
        public EventWrapper(EventDef @event)
        {
            Name = @event.Name;
            TypeFullName = @event.EventType.FullName;
        }

        public string Name { get; }

        public string TypeFullName { get; }
    }

    class MethodWrapper : IAvVSMethodInformation
    {
        private readonly MethodDef _method;
        private readonly Lazy<IList<IParameterInformation>> _parameters;

        public MethodWrapper(MethodDef method)
        {
            _method = method;
            _parameters = new Lazy<IList<IParameterInformation>>(() =>
                _method.Parameters.Skip(_method.IsStatic ? 0 : 1).Select(p => (IParameterInformation)new ParameterWrapper(p)).ToList() as
                    IList<IParameterInformation>);
        }

        public bool IsStatic => _method.IsStatic;

        public bool IsPublic => _method.IsPublic;

        public string Name => _method.Name;

        public IList<IParameterInformation> Parameters => _parameters.Value;

        public string ReturnTypeFullName => _method.ReturnType?.FullName;

        public override string ToString() => Name;
    }

    class ParameterWrapper : IParameterInformation
    {
        private readonly Parameter _param;

        public ParameterWrapper(Parameter param)
        {
            _param = param;
        }

        public string TypeFullName => _param.Type.FullName;
    }

    static class WrapperExtensions
    {
        public static bool IsPublicOrInternal(this MethodDef methodDef)
                            => methodDef?.IsPublic == true || methodDef?.IsAssembly == true;
    }
}
