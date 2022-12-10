namespace Avalonia.Ide.CompletionEngine
{
    /// <summary>
    /// Represents edit to be applied to textbuffer
    /// For simplicity sake two types of manipulations are offered only - Insertion and Deletion
    /// </summary>
    public class TextManipulation
    {
        public static TextManipulation Insert(int postition, string text)
        {
            return new TextManipulation(postition, postition + text.Length, text, ManipulationType.Insert);
        }

        public static TextManipulation Delete(int postition, int length)
        {
            return new TextManipulation(postition, postition + length, null, ManipulationType.Delete);
        }

        private TextManipulation(int start, int end, string text, ManipulationType type)
        {
            Start = start;
            End = end;
            Text = text;
            Type = type;
        }

        public int Start { get; }

        public int End { get; }

        public string Text { get; }

        public ManipulationType Type { get; }
    }
}
