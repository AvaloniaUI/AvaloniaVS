using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using dnlib.DotNet;

namespace Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;

internal class AssemblyWrapper : IAssemblyInformation
{
    private readonly AssemblyDef _asm;
    private readonly DnlibMetadataProviderSession _session;

    public AssemblyWrapper(AssemblyDef asm, DnlibMetadataProviderSession session)
    {
        _asm = asm;
        _session = session;
    }

    public string Name => _asm.Name;

    public string AssemblyName
        => _asm.GetFullNameWithPublicKeyToken();

    public IEnumerable<ITypeInformation> Types
        => _asm.Modules.SelectMany(m => m.Types).Select(x => TypeWrapper.FromDef(x, _session)).Where(t => t is not null)!;

    public IEnumerable<ICustomAttributeInformation> CustomAttributes
        => _asm.CustomAttributes.Select(a => new CustomAttributeWrapper(a));

    public IEnumerable<string> ManifestResourceNames
        => _asm.ManifestModule.Resources.Select(r => r.Name.ToString());

    public IEnumerable<string> InternalsVisibleTo
        => _asm.GetVisibleTo();

    public Stream GetManifestResourceStream(string name)
        => _asm.ManifestModule.Resources.FindEmbeddedResource(name).CreateReader().AsStream();

    public string PublicKey
        => _asm.PublicKey.ToString();

    public override string ToString() => Name;
}

internal class TypeWrapper : ITypeInformation
{
    private readonly TypeDef _type;
    private readonly DnlibMetadataProviderSession _session;

    public static TypeWrapper? FromDef(TypeDef? def, DnlibMetadataProviderSession session) => def == null ? null : new TypeWrapper(def, session);

    private TypeWrapper(TypeDef type, DnlibMetadataProviderSession session)
    {
        if (type == null)
            throw new ArgumentNullException();
        _type = type;
        _session = session;
        AssemblyQualifiedName = _type.AssemblyQualifiedName;
    }

    public string FullName => _type.FullName;
    public string Name => _type.Name;
    public string AssemblyQualifiedName { get; }
    public string Namespace => _type.Namespace;
    public ITypeInformation? GetBaseType() => FromDef(_session.GetTypeDef(_session.GetBaseType(_type)), _session);

    public IEnumerable<IEventInformation> Events => _type.Events.Select(EventWrapper.FromDef);

    public IEnumerable<IMethodInformation> Methods => _type.GetMethodsHierarchy().Select(m => MethodWrapper.FromDef(m, _session));

    public IEnumerable<IFieldInformation> Fields => _type.Fields.Select(f => new FieldWrapper(f, _session));

    public IEnumerable<IPropertyInformation> Properties => _type.Properties
        //Filter indexer properties
        .Where(p =>
            (p.GetMethod?.IsPublicOrInternal() == true && p.GetMethod.Parameters.Count == (p.GetMethod.IsStatic ? 0 : 1))
            || (p.SetMethod?.IsPublicOrInternal() == true && p.SetMethod.Parameters.Count == (p.SetMethod.IsStatic ? 1 : 2)))
        // Filter property overrides
        .Where(p => !p.Name.Contains("."))
        .Select(PropertyWrapper.FromDef);
    public bool IsEnum => _type.IsEnum;
    public bool IsStatic => _type.IsAbstract && _type.IsSealed;
    public bool IsInterface => _type.IsInterface;
    public bool IsPublic => _type.IsPublic;
    public bool IsGeneric => _type.HasGenericParameters;
    public bool IsAbstract => _type.IsAbstract && !_type.IsSealed;
    public bool IsInternal => _type.IsNotPublic && !_type.IsNestedPrivate;

    public IEnumerable<string> EnumValues
    {
        get
        {
            return _type.Fields.Where(f => f.IsStatic).Select(f => f.Name.String).ToArray();
        }
    }
    public IEnumerable<string> Pseudoclasses
    {
        get
        {
            // There is probably a much nicer way to do this, but it works
            // Would be nice if we had a ref to the PseudoClassesAttribute to just pull
            // the values from though...
            var attr = _type.CustomAttributes
                .Where(x => x.TypeFullName.Contains("PseudoClassesAttribute"));

            var selector = attr.Select(x =>
            {
                if (x.HasConstructorArguments)
                {
                    return (x.ConstructorArguments[0].Value as IEnumerable<CAArgument>)?
                            .Select(y => y.Value.ToString()) ?? Enumerable.Empty<string>();
                }

                return Enumerable.Empty<string>();
            });

            foreach (var ret in selector)
                foreach (var ret2 in ret)
                    if (ret2 is not null)
                        yield return ret2;
        }
    }
    public override string ToString() => Name;

    public bool IsSubclassOf(ITypeInformation? type)
    {
        if (type is TypeWrapper wrapper)
        {
            return wrapper._type.IsAssignableFrom(_type);
        }
        return false;
    }

    public IEnumerable<ITypeInformation> NestedTypes =>
        _type.HasNestedTypes ? _type.NestedTypes.Select(t => new TypeWrapper(t, _session)) : Array.Empty<TypeWrapper>();

    public IEnumerable<(ITypeInformation Type, string Name)> TemplateParts
    {
        get
        {
            var attributes = _type.CustomAttributes
                .Where(a => a.TypeFullName.EndsWith("TemplatePartAttribute", StringComparison.OrdinalIgnoreCase)
                    && a.HasConstructorArguments);
            foreach (var attr in attributes)
            {
                var name = attr.ConstructorArguments[0].Value.ToString()!;
                ITypeInformation type = FromDef(((ClassSig)attr.ConstructorArguments[1].Value).TypeDef, _session)!;
                yield return (type, name);
            }
        }
    }
    IReadOnlyList<ICustomAttributeInformation>? _customAttributes;
    public IReadOnlyList<ICustomAttributeInformation> CustomAttributes
    {
        get
        {
            if (_customAttributes is null)
            {
                _customAttributes = _type.CustomAttributes
                    .Select(a => new CustomAttributeWrapper(a))
                    .ToArray();
            }
            return _customAttributes!;
        }
    }
}

internal class CustomAttributeWrapper : ICustomAttributeInformation
{
    private readonly Lazy<IList<IAttributeConstructorArgumentInformation>> _args;
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

internal class ConstructorArgumentWrapper : IAttributeConstructorArgumentInformation
{
    public ConstructorArgumentWrapper(CAArgument ca)
    {
        if (ca.Value is ClassSig cs)
        {
            Value = cs.AssemblyQualifiedName;
        }
        else
        {
            Value = ca.Value;
        }
    }

    public object? Value { get; }
}

internal class PropertyWrapper : IPropertyInformation
{
    private readonly PropertyDef _prop;
    private readonly Func<PropertyDef, IAssemblyInformation, bool> _isVisbleTo;
    private readonly static ConcurrentDictionary<PropertyDef, PropertyWrapper> _propertiesCache = new();

    public static PropertyWrapper FromDef(PropertyDef def) =>
        _propertiesCache.GetOrAdd(def, new PropertyWrapper(def));

    private PropertyWrapper(PropertyDef prop)
    {
        Name = prop.Name;

        var setMethod = prop.SetMethod;
        var getMethod = prop.GetMethod;

        IsStatic = setMethod?.IsStatic ?? getMethod?.IsStatic ?? false;
        IsPublic = prop.IsPublic();

        HasPublicSetter = setMethod?.IsPublic() ?? false;
        HasPublicGetter = getMethod?.IsPublic() ?? false;

        TypeSig? type = default;
        if (getMethod is not null)
        {
            type = getMethod.ReturnType;
        }
        else if (setMethod is not null)
        {
            type = setMethod.Parameters[setMethod.IsStatic ? 0 : 1].Type;
        }
        else
        {
            throw new InvalidOperationException("Property without a type was found.");
        }

        TypeFullName = type.FullName;
        QualifiedTypeFullName = type.AssemblyQualifiedName;

        _prop = prop;
        IsContent = _prop.CustomAttributes.Any(a => a.TypeFullName == "Avalonia.Metadata.ContentAttribute");
        if (HasPublicGetter || HasPublicSetter)
        {
            _isVisbleTo = static (_, _) => true;
        }
        else
        {
            _isVisbleTo = static (property, targetAssembly) =>
            {
                if (property.DeclaringType.DefinitionAssembly is AssemblyDef assembly)
                {
                    if (string.Equals(targetAssembly.AssemblyName, assembly.GetFullNameWithPublicKeyToken(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    var enumerator = assembly.GetVisibleTo()?.GetEnumerator();
                    var targetPublicKey = targetAssembly.PublicKey;
                    var targetName = targetAssembly.Name;
                    while (enumerator?.MoveNext() == true)
                    {
                        var current = enumerator.Current;
                        if (current.StartsWith(targetName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(targetPublicKey))
                            {
                                var startIndex = current.IndexOf("PublicKey", StringComparison.OrdinalIgnoreCase);
                                if (startIndex > -1)
                                {
                                    startIndex += 9;
                                    if (startIndex > current.Length)
                                    {
                                        return false;
                                    }
                                    while (startIndex < current.Length && current[startIndex] is ' ' or '=')
                                    {
                                        startIndex++;
                                    }

                                    if (targetPublicKey.Length != current.Length - startIndex)
                                    {
                                        return false;
                                    }
                                    for (int i = startIndex; i < current.Length; i++)
                                    {
                                        if (current[i] != targetPublicKey[i - startIndex])
                                        {
                                            return false;
                                        }
                                    }
                                }
                            }
                            return true;
                        }
                    }
                }
                return false;
            };
        }
    }

    public bool IsStatic { get; }
    public bool IsPublic { get; }
    public bool IsInternal { get; }
    public bool HasPublicSetter { get; }
    public bool HasPublicGetter { get; }
    public string TypeFullName { get; }
    public string QualifiedTypeFullName { get; }
    public string Name { get; }

    public bool IsContent { get; }

    public bool IsVisbleTo(IAssemblyInformation assembly) =>
        _isVisbleTo(_prop, assembly);

    public override string ToString() => Name;
}

internal class FieldWrapper : IFieldInformation
{
    public FieldWrapper(FieldDef f, DnlibMetadataProviderSession session)
    {
        IsStatic = f.IsStatic;
        IsPublic = f.IsPublic || f.IsAssembly;
        Name = f.Name;
        ReturnTypeFullName = f.FieldType.FullName;
        QualifiedTypeFullName = f.FieldType.AssemblyQualifiedName;
        bool isRoutedEvent = false;
        ITypeDefOrRef t = f.FieldType.ToTypeDefOrRef();
        while (t != null)
        {
            if (t.Name == "RoutedEvent" && t.Namespace == "Avalonia.Interactivity")
            {
                isRoutedEvent = true;
                break;
            }
            t = session.GetBaseType(t);
        }

        IsRoutedEvent = isRoutedEvent;
    }

    public bool IsRoutedEvent { get; }

    public bool IsStatic { get; }

    public bool IsPublic { get; }

    public string Name { get; }

    public string ReturnTypeFullName { get; }
    public string QualifiedTypeFullName { get; }
}

internal class EventWrapper : IEventInformation
{
    readonly static ConcurrentDictionary<EventDef, EventWrapper> _eventsCache = new();

    public static EventWrapper FromDef(EventDef def) =>
             _eventsCache.GetOrAdd(def, new EventWrapper(def));

    private EventWrapper(EventDef @event)
    {
        Name = @event.Name;
        TypeFullName = @event.EventType.FullName;
        QualifiedTypeFullName = @event.EventType.AssemblyQualifiedName;
        IsPublic = @event.IsPublic();
        IsInternal = @event.IsInternal();
    }

    public string Name { get; }

    public string TypeFullName { get; }
    public string QualifiedTypeFullName { get; }
    public bool IsPublic { get; }
    public bool IsInternal { get; }
}

internal class MethodWrapper : IMethodInformation
{
    private readonly MethodDef _method;
    private readonly DnlibMetadataProviderSession _session;
    private readonly Lazy<IList<IParameterInformation>> _parameters;
    private static readonly ConcurrentDictionary<MethodDef, MethodWrapper> _methodsCache = new();

    public static MethodWrapper FromDef(MethodDef def, DnlibMetadataProviderSession session) =>
            _methodsCache.GetOrAdd(def, new MethodWrapper(def, session));

    private MethodWrapper(MethodDef method, DnlibMetadataProviderSession session)
    {
        _method = method;
        _session = session;
        _parameters = new Lazy<IList<IParameterInformation>>(() =>
            _method.Parameters.Skip(_method.IsStatic ? 0 : 1).Select(p => (IParameterInformation)new ParameterWrapper(p, _session!)).ToList());
        if (_method.ReturnType is not null)
        {
            QualifiedReturnTypeFullName = _method.ReturnType.AssemblyQualifiedName;
            ReturnTypeFullName = _method.ReturnType.FullName;
        }
        else
        {
            QualifiedReturnTypeFullName = typeof(void).AssemblyQualifiedName!;
            ReturnTypeFullName = typeof(void).FullName!;
        }
    }

    public bool IsStatic => _method.IsStatic;
    public bool IsPublic => _method.IsPublic;
    public string Name => _method.Name;
    public IList<IParameterInformation> Parameters => _parameters.Value;
    public string ReturnTypeFullName { get; }
    public string QualifiedReturnTypeFullName { get; }

    private IReadOnlyList<ICustomAttributeInformation>? _customAttributes;
    public IReadOnlyList<ICustomAttributeInformation> CustomAttributes
    {
        get
        {
            if (_customAttributes == null)
            {
                _customAttributes = _method.CustomAttributes
                    .Select(a => new CustomAttributeWrapper(a))
                    .ToArray();
            }
            return _customAttributes;
        }
    }

    public override string ToString() => Name;
}

internal class ParameterWrapper : IParameterInformation
{
    private readonly Lazy<ITypeInformation?> _type;
    private readonly DnlibMetadataProviderSession _session;

    public ParameterWrapper(Parameter param, DnlibMetadataProviderSession session)
    {
        TypeFullName = param.Type.FullName;
        QualifiedTypeFullName = param.Type.AssemblyQualifiedName;
        _type = new Lazy<ITypeInformation?>(() =>
        {
            if (param.Type?.TryGetTypeDefOrRef()?.ResolveTypeDef() is { } td)
            {
                return TypeWrapper.FromDef(td, _session!);
            }
            return null;
        });
        _session = session;
    }
    public string TypeFullName { get; }
    public string QualifiedTypeFullName { get; }
    public ITypeInformation? Type => _type.Value;
}
