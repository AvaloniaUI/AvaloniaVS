namespace Avalonia.Ide.CompletionEngine
{
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
        AttachedEvent
    }

    public class Completion
    {
        public string DisplayText { get; }

        public string InsertText { get; }

        public string Description { get; }

        public CompletionKind Kind { get; }

        public int? RecommendedCursorOffset { get; }

        public Completion(string displayText, string insertText, string description, CompletionKind kind, int? recommendedCursorOffset = null)
        {
            DisplayText = displayText;
            InsertText = insertText;
            Description = description;
            Kind = kind;
            RecommendedCursorOffset = recommendedCursorOffset;
        }

        public override string ToString() => DisplayText;

        public Completion(string insertText, CompletionKind kind) : this(insertText, insertText, insertText, kind)
        {

        }
    }
}
