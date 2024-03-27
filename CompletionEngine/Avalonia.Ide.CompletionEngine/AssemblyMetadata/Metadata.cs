using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Avalonia.Ide.CompletionEngine;

public class Metadata
{
    readonly Dictionary<string, Dictionary<string, MetadataType>> _namespaces = new();
    readonly Dictionary<string, string> _inverseNamespace = new();

    public IReadOnlyDictionary<string, Dictionary<string, MetadataType>> Namespaces => _namespaces;
    public IReadOnlyDictionary<string, string> InverseNamespace => _inverseNamespace;

    public void AddType(string ns, MetadataType type)
    {
        _namespaces.GetOrCreate(ns)[type.Name] = type;
        _inverseNamespace[type.FullName] = ns;
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
}

// todo: add property for permutation annotation. A MetadataType may be defined in multiple build contexts, but have different definitions.
[DebuggerDisplay("{Name}")]
public record MetadataType(string Name)
{
    public bool IsEnum { get; set; }
    public bool IsMarkupExtension { get; set; }
    public bool IsStatic { get; set; }
    public bool HasHintValues { get; set; }
    public string[]? HintValues { get; set; }

    public string[] PseudoClasses { get; set; } = Array.Empty<string>();
    public bool HasPseudoClasses { get; set; }

    //assembly, type, property
    public Func<string?, MetadataType, MetadataProperty?, bool>? IsValidForXamlContextFunc { get; set; }
    //assembly, type, property
    public Func<string?, MetadataType, MetadataProperty?, IEnumerable<string>>? XamlContextHintValuesFunc { get; set; }
    public string FullName { get; set; } = "";
    public List<MetadataProperty> Properties { get; set; } = new List<MetadataProperty>();
    public List<MetadataEvent> Events { get; set; } = new List<MetadataEvent>();
    public bool HasAttachedProperties { get; set; }
    public bool HasAttachedEvents { get; set; }
    public bool HasStaticGetProperties { get; set; }
    public bool HasSetProperties { get; set; }
    public bool IsAvaloniaObjectType { get; set; }
    public MetadataTypeCtorArgument SupportCtorArgument { get; set; }
    public bool IsCompositeValue { get; set; }
    public bool IsGeneric { get; set; }
    public bool IsXamlDirective { get; set; }
    public string? AssemblyQualifiedName { get; set; }
    public bool IsNullable { get; init; }
    public MetadataType? UnderlyingType { get; init; }
    public List<(MetadataType Type, string Name)> TemplateParts { get; set; } = new List<(MetadataType Type, string Name)>();
    public bool IsAbstract { get; internal set; } = false;
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
public record MetadataProperty(string Name, MetadataType? Type, MetadataType? DeclaringType, bool IsAttached, bool IsStatic, bool HasGetter, bool HasSetter);

public record MetadataEvent(string Name, MetadataType? Type, MetadataType? DeclaringType, bool IsAttached);
