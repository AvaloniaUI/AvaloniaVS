using System;

namespace Avalonia.Ide.CompletionEngine;

[Flags]
public enum CompletionKind
{
    None = 0x0,
    Comment = 0x1,
    Class = 0x2,
    Property = 0x4,
    AttachedProperty = 0x8,
    StaticProperty = 0x10,
    Namespace = 0x20,
    Enum = 0x40,
    MarkupExtension = 0x80,
    Event = 0x100,
    AttachedEvent = 0x200,
    
    /// <summary>
    /// Properties from DataContexts (view models), specifically this is for VS
    /// to use a different icon from normal properties
    /// </summary>
    DataProperty = 0x200,

    /// <summary>
    /// Classes when listed from TargetType or Selector, specfically for VS to use
    /// a different icon from <see cref="Class"/> used in tag names
    /// </summary>
    TargetTypeClass = 0x400,

    /// <summary>
    /// xmlns list in visual studio (uses enum icon instead of namespace icon)
    /// </summary>
    VS_XMLNS = 0x800,

    Selector = 0x1000,
    Name = 0x2000,
}

public record Completion(string DisplayText,
    string InsertText,
    string Description,
    CompletionKind Kind,
    int? RecommendedCursorOffset = null,
    string? Suffix = null,
    int? DeleteTextOffset = null
    )
{
    public override string ToString() => DisplayText;

    public Completion(string insertText, CompletionKind kind, string? suffix = default) :
        this(insertText, insertText, insertText, kind, Suffix: suffix)
    {

    }

    public Completion(string displayText, string insertText, CompletionKind kind, string? suffix = default) :
        this(displayText, insertText, displayText, kind)
    {

    }
}
