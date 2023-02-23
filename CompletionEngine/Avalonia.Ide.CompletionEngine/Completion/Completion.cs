namespace Avalonia.Ide.CompletionEngine;

public enum CompletionKind
{
    None,
    Class,
    Property,
    AttachedProperty,
    StaticProperty,
    Namespace,
    Enum,
    MarkupExtension,
    Event,
    AttachedEvent,

    /// <summary>
    /// Properties from DataContexts (view models), specifically this is for VS
    /// to use a different icon from normal properties
    /// </summary>
    DataProperty
}

public record Completion(string DisplayText, string InsertText, string Description, CompletionKind Kind, int? RecommendedCursorOffset = null)
{
    public override string ToString() => DisplayText;

    public Completion(string insertText, CompletionKind kind) : this(insertText, insertText, insertText, kind)
    {

    }
}
