namespace Avalonia.Ide.CompletionEngine;

/// <summary>
/// Represents edit to be applied to textbuffer
/// For simplicity sake two types of manipulations are offered only - Insertion and Deletion
/// </summary>
public record TextManipulation(int Start, int End, string? Text, ManipulationType Type)
{
    public static TextManipulation Insert(int postition, string text)
    {
        return new TextManipulation(postition, postition + text.Length, text, ManipulationType.Insert);
    }

    public static TextManipulation Delete(int postition, int length)
    {
        return new TextManipulation(postition, postition + length, null, ManipulationType.Delete);
    }
}
