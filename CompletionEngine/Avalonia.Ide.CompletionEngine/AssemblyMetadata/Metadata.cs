using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Avalonia.Ide.CompletionEngine
{
    public class Metadata
    {
        public Dictionary<string, Dictionary<string, MetadataType>> Namespaces { get; } = new Dictionary<string, Dictionary<string, MetadataType>>();

        public void AddType(string ns, MetadataType type) => Namespaces.GetOrCreate(ns)[type.Name] = type;
    }

    [DebuggerDisplay("{Name}")]
    public record MetadataType(string Name)
    {
        public bool IsEnum { get; set; }
        public bool IsMarkupExtension { get; set; }
        public bool IsStatic { get; set; }
        public bool HasHintValues { get; set; }
        public string[]? HintValues { get; set; }

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
}
