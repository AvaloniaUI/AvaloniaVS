using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using dnlib.DotNet;

namespace Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;

internal class AssemblyWrapper : IAssemblyInformation
{
    private readonly WeakReference<AssemblyDef> _asm;
    private readonly WeakReference<DnlibMetadataProviderSession> _session;

    public AssemblyWrapper(AssemblyDef asm, DnlibMetadataProviderSession session)
    {
        _asm = new(asm);
        _session = new(session);
        Name = asm.Name;
        AssemblyName = asm.GetFullNameWithPublicKeyToken();
        PublicKey = asm.PublicKey.ToString();
    }

    public string Name { get; }

    public string AssemblyName { get; }

    public IEnumerable<ITypeInformation> Types
    {
        get
        {
            if (_session.TryGetTarget(out var session))
            {
                if (_asm.TryGetTarget(out var asm))
                {
                    return asm.Modules.SelectMany(m => m.Types).Select(x => TypeWrapper.FromDef(x, session)).Where(t => t is not null)!;
                }
            }
            throw new ObjectDisposedException("session");
        }
    }

    public IEnumerable<ICustomAttributeInformation> CustomAttributes
    {
        get
        {
            if (_asm.TryGetTarget(out var asm))
            {
                return asm.CustomAttributes.Select(a => new CustomAttributeWrapper(a));
            }
            throw new ObjectDisposedException("session");
        }
    }

    public IEnumerable<string> ManifestResourceNames
    {
        get
        {
            if (_asm.TryGetTarget(out var asm))
            {
                return asm.ManifestModule.Resources.Select(r => r.Name.ToString());
            }
            throw new ObjectDisposedException("session");
        }
    }

    public IEnumerable<string> InternalsVisibleTo
    {
        get
        {
            if (_asm.TryGetTarget(out var asm))
            {
                return asm.GetVisibleTo();
            }
            throw new ObjectDisposedException("session");
        }
    }

    public Stream GetManifestResourceStream(string name)
    {
        if (_asm.TryGetTarget(out var asm))
        {
            return asm.ManifestModule.Resources.FindEmbeddedResource(name).CreateReader().AsStream();
        }
        throw new ObjectDisposedException("session");
    }

    public string PublicKey { get; }

    public override string ToString() => Name;
}

internal class TypeWrapper : ITypeInformation
{
    private readonly WeakReference<TypeDef> _type;
    private readonly WeakReference<DnlibMetadataProviderSession> _session;

    public static TypeWrapper? FromDef(TypeDef? def, DnlibMetadataProviderSession session) => def == null ? null : new TypeWrapper(def, session);

    private TypeWrapper(TypeDef type, DnlibMetadataProviderSession session)
    {
        if (type == null)
            throw new ArgumentNullException();
        _type = new(type);
        _session = new(session);
        AssemblyQualifiedName = type.AssemblyQualifiedName;
        FullName = type.FullName;
        Name = type.Name;
        Namespace = type.Namespace;
        IsEnum = type.IsEnum;
        IsStatic = type.IsAbstract && type.IsSealed;
        IsInterface = type.IsInterface;
        IsPublic = type.IsPublic;
        IsGeneric = type.HasGenericParameters;
        IsAbstract = type.IsAbstract && !type.IsSealed;
        IsInternal = type.IsNotPublic && !type.IsNestedPrivate;
    }

    public string FullName { get; }
    public string Name { get; }
    public string AssemblyQualifiedName { get; }
    public string Namespace { get; }
    public ITypeInformation? GetBaseType()
    {
        if (_session.TryGetTarget(out var session) && _type.TryGetTarget(out var type))
        {
            return FromDef(session.GetTypeDef(session.GetBaseType(type)), session);
        }
        throw new ObjectDisposedException("session");
    }

    public IEnumerable<IEventInformation> Events
    {
        get
        {
            if (_session.TryGetTarget(out var session) && _type.TryGetTarget(out var type))
            {
                return type.Events.Select(e => EventWrapper.FromDef(e, session));
            }
            throw new ObjectDisposedException("session");
        }
    }

    public IEnumerable<IMethodInformation> Methods
    {
        get
        {
            if (_session.TryGetTarget(out var session) && _type.TryGetTarget(out var type))
            {
                return type.GetMethodsHierarchy().Select(m => MethodWrapper.FromDef(m, session));
            }
            throw new ObjectDisposedException("session");
        }
    }

    public IEnumerable<IFieldInformation> Fields
    {
        get
        {
            if (_session.TryGetTarget(out var session) && _type.TryGetTarget(out var type))
            {
                return type.Fields.Select(f => new FieldWrapper(f, session));
            }
            throw new ObjectDisposedException("session");
        }
    }

    public IEnumerable<IPropertyInformation> Properties
    {
        get
        {
            if (_session.TryGetTarget(out var session) && _type.TryGetTarget(out var type))
            {
                return type.Properties
                    //Filter indexer properties
                    .Where(p =>
                        (p.GetMethod?.IsPublicOrInternal() == true && p.GetMethod.Parameters.Count == (p.GetMethod.IsStatic ? 0 : 1))
                        || (p.SetMethod?.IsPublicOrInternal() == true && p.SetMethod.Parameters.Count == (p.SetMethod.IsStatic ? 1 : 2)))
                     // Filter property overrides
                     .Where(p => !p.Name.Contains("."))
                    .Select(p => PropertyWrapper.FromDef(p, session));
            }
            throw new ObjectDisposedException("session");
        }
    }

    public bool IsEnum { get; }
    public bool IsStatic { get; }
    public bool IsInterface { get; }
    public bool IsPublic { get; }
    public bool IsGeneric { get; }
    public bool IsAbstract { get; }
    public bool IsInternal { get; }

    public IEnumerable<string> EnumValues
    {
        get
        {
            if (_type.TryGetTarget(out var type))
            {
                return type.Fields.Where(f => f.IsStatic).Select(f => f.Name.String).ToArray();
            }
            throw new ObjectDisposedException("session");
        }
    }
    public IEnumerable<string> Pseudoclasses
    {
        get
        {
            // There is probably a much nicer way to do this, but it works
            // Would be nice if we had a ref to the PseudoClassesAttribute to just pull
            // the values from though...
            if (!_type.TryGetTarget(out var type))
            {
                throw new ObjectDisposedException("session");
            }
            var attr = type.CustomAttributes
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
        if (!_type.TryGetTarget(out var thisType))
        {
            throw new ObjectDisposedException("session");
        }
        if (type is TypeWrapper wrapper && wrapper._type.TryGetTarget(out var otherType))
        {
            return otherType.IsAssignableFrom(thisType);
        }
        return false;
    }

    public IEnumerable<ITypeInformation> NestedTypes
    {
        get
        {
            if (_session.TryGetTarget(out var session) && _type.TryGetTarget(out var type))
            {
                return type.HasNestedTypes ? type.NestedTypes.Select(t => new TypeWrapper(t, session)) : Array.Empty<TypeWrapper>();
            }
            throw new ObjectDisposedException("session");
        }
    }

    public IEnumerable<(ITypeInformation Type, string Name)> TemplateParts
    {
        get
        {
            if (!_type.TryGetTarget(out var type))
            {
                throw new ObjectDisposedException("session");
            }
            var attributes = type.CustomAttributes
                .Where(a => a.TypeFullName.EndsWith("TemplatePartAttribute", StringComparison.OrdinalIgnoreCase)
                    && a.HasConstructorArguments);
            foreach (var attr in attributes)
            {
                if (_session.TryGetTarget(out var session))
                {
                    var name = attr.ConstructorArguments[0].Value.ToString()!;
                    var def = session.GetTypeDef(((ClassSig)attr.ConstructorArguments[1].Value).TypeDefOrRef);
                    ITypeInformation t = FromDef(def, session)!;
                    yield return (t, name);
                }
                else
                {
                    throw new ObjectDisposedException("session");
                }
            }

        }
    }
    IReadOnlyList<ICustomAttributeInformation>? _customAttributes;
    public IReadOnlyList<ICustomAttributeInformation> CustomAttributes
    {
        get
        {
            if (!_type.TryGetTarget(out var type))
            {
                throw new ObjectDisposedException("session");
            }
            if (_customAttributes is null)
            {
                _customAttributes = type.CustomAttributes
                    .Select(a => new CustomAttributeWrapper(a))
                    .ToArray();
            }
            return _customAttributes!;
        }
    }
}

internal class CustomAttributeWrapper : ICustomAttributeInformation
{
    private readonly IList<IAttributeConstructorArgumentInformation> _args;
    public CustomAttributeWrapper(CustomAttribute attr)
    {
        TypeFullName = attr.TypeFullName;
        _args = attr.ConstructorArguments
            .Select(ca => (IAttributeConstructorArgumentInformation)new ConstructorArgumentWrapper(ca)).ToList();
    }

    public string TypeFullName { get; }
    public IList<IAttributeConstructorArgumentInformation> ConstructorArguments => _args;
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

    public static PropertyWrapper FromDef(PropertyDef def, DnlibMetadataProviderSession session) =>
        session._propertiesCache.GetOrAdd(def, new PropertyWrapper(def));

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
    public static EventWrapper FromDef(EventDef def, DnlibMetadataProviderSession session) =>
             session._eventsCache.GetOrAdd(def, new EventWrapper(def));

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
    private readonly IList<IParameterInformation> _parameters;

    public static MethodWrapper FromDef(MethodDef def, DnlibMetadataProviderSession session) =>
            session._methodsCache.GetOrAdd(def, new MethodWrapper(def, session));

    private MethodWrapper(MethodDef method, DnlibMetadataProviderSession session)
    {
        _parameters = method.Parameters.Skip(method.IsStatic ? 0 : 1)
                    .Select(p => (IParameterInformation)new ParameterWrapper(p, session))
                    .ToList();

        if (method.ReturnType is not null)
        {
            QualifiedReturnTypeFullName = method.ReturnType.AssemblyQualifiedName;
            ReturnTypeFullName = method.ReturnType.FullName;
        }
        else
        {
            QualifiedReturnTypeFullName = typeof(void).AssemblyQualifiedName!;
            ReturnTypeFullName = typeof(void).FullName!;
        }
        IsStatic = method.IsStatic;
        IsPublic = method.IsPublic;
        Name = method.Name;
        CustomAttributes = method.CustomAttributes
                    .Select(a => new CustomAttributeWrapper(a))
                    .ToArray();
    }

    public bool IsStatic { get; }
    public bool IsPublic { get; }
    public string Name { get; }
    public IList<IParameterInformation> Parameters => _parameters;
    public string ReturnTypeFullName { get; }
    public string QualifiedReturnTypeFullName { get; }

    public IReadOnlyList<ICustomAttributeInformation> CustomAttributes { get; }

    public override string ToString() => Name;
}

internal class ParameterWrapper : IParameterInformation
{
    private readonly ITypeInformation? _type;
    public ParameterWrapper(Parameter param, DnlibMetadataProviderSession session)
    {
        TypeFullName = param.Type.FullName;
        QualifiedTypeFullName = param.Type.AssemblyQualifiedName;
        _type = param.Type?.TryGetTypeDefOrRef()?.ResolveTypeDef() is { } td
            ? TypeWrapper.FromDef(td, session)
            : default;
    }
    public string TypeFullName { get; }
    public string QualifiedTypeFullName { get; }
    public ITypeInformation? Type => _type;
}
