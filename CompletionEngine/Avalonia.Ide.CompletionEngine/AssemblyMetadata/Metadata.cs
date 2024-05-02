using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;

namespace Avalonia.Ide.CompletionEngine;

public class Metadata : IDisposable
{
    readonly Dictionary<string, Dictionary<string, MetadataType>> _namespaces = new();
    readonly Dictionary<string, ISet<string>> _inverseNamespace = new();

    public IReadOnlyDictionary<string, Dictionary<string, MetadataType>> Namespaces => _namespaces;
    public IReadOnlyDictionary<string, ISet<string>> InverseNamespace => _inverseNamespace;

    public void AddType(string ns, MetadataType type)
    {
        _namespaces.GetOrCreate(ns)[type.Name] = type;

        var namespaces = _inverseNamespace.GetOrCreate(type.FullName, _ => new HashSet<string>());
        namespaces.Add(ns);
    }

    /// <summary>
    /// Add new metadata. Keys are added and existing keys are unchanged.
    /// </summary>
    public void AddMetadata(Metadata metadata)
    {
        foreach (var x in metadata._namespaces)
            if (!_namespaces.ContainsKey(x.Key))
                _namespaces.Add(x.Key, x.Value);
        foreach (var x in metadata._inverseNamespace)
            if (!_inverseNamespace.ContainsKey(x.Key))
                _inverseNamespace.Add(x.Key, x.Value);
    }

    public void Dispose()
    {
         var types = _namespaces.Values
            .SelectMany(d=>d.Values)
            .ToArray();
        _namespaces.Clear();
        _inverseNamespace.Clear();
        foreach (var type in types)
        {
            type.Dispose();
        }
    }
}

// todo: add property for permutation annotation. A MetadataType may be defined in multiple build contexts, but have different definitions.
[DebuggerDisplay("{Name}")]
public record MetadataType(string Name) : IDisposable
{
    public bool IsEnum { get; internal set; }
    public bool IsMarkupExtension { get; internal set; }
    public bool IsStatic { get; internal set; }
    public bool HasHintValues { get; internal set; }
    public string[]? HintValues { get; internal set; }


    public string[] PseudoClasses { get; internal set; } = Array.Empty<string>();
    public bool HasPseudoClasses { get; internal set; }

    //assembly, type, property
    public Func<string?, MetadataType, MetadataProperty?, bool>? IsValidForXamlContextFunc { get; internal set; }
    //assembly, type, property
    public Func<string?, MetadataType, MetadataProperty?, IEnumerable<string>>? XamlContextHintValuesFunc { get; internal set; }
    public string FullName { get; set; } = "";
    public List<MetadataProperty> Properties { get; internal set; } = new List<MetadataProperty>();
    public List<MetadataEvent> Events { get; internal set; } = new List<MetadataEvent>();
    public bool HasAttachedProperties { get; internal set; }
    public bool HasAttachedEvents { get; internal set; }
    public bool HasStaticGetProperties { get; internal set; }
    public bool HasSetProperties { get; internal set; }
    public bool IsAvaloniaObjectType { get; internal set; }
    public MetadataTypeCtorArgument SupportCtorArgument { get; internal set; }
    public bool IsCompositeValue { get; internal set; }
    public bool IsGeneric { get; internal set; }
    public bool IsXamlDirective { get; internal set; }
    public string? AssemblyQualifiedName { get; internal set; }
    public bool IsNullable { get; init; }
    public MetadataType? UnderlyingType { get; init; }
    public List<(MetadataType Type, string Name)> TemplateParts { get; internal set; } = new List<(MetadataType Type, string Name)>();
    public bool IsAbstract { get; internal set; } = false;
    public MetadataType? ItemsType { get; internal set; }

    internal ITypeInformation? Type { get; set; }

    public void Dispose()
    {
        (Type as IDisposable)?.Dispose();
        Type = null;
        TemplateParts.Clear();
        XamlContextHintValuesFunc = null;
        IsValidForXamlContextFunc = null;
        Events.Clear();
        Properties.Clear();
    }

    internal bool IsSubclassOf(MetadataType? other)
    {
        if (ReferenceEquals(Type, other?.Type))
            return true;
        if (Type is null)
            return false;
        return Type.IsSubclassOf(other?.Type);
    }

}

public enum MetadataTypeCtorArgument
{
    None,
    Type,
    Object,
    TypeAndObject,
    HintValues
}

[DebuggerDisplay("{Name} from {DeclaringType}")]
public record MetadataProperty(string Name, MetadataType? Type, MetadataType? DeclaringType, bool IsAttached, bool IsStatic, bool HasGetter, bool HasSetter, bool IsContent);

public record MetadataEvent(string Name, MetadataType? Type, MetadataType? DeclaringType, bool IsAttached);
