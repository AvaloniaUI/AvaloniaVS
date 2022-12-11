using System.Text;

namespace Avalonia.Ide.CompletionEngine
{
    /// <summary>
    /// Abstracts text change from editor
    /// </summary>
    public interface ITextChange
    {
        /// <summary>
        /// Position of new text
        /// </summary>
        int NewPosition { get; }

        /// <summary>
        /// Text that replaced old text
        /// </summary>
        string NewText { get; }

        /// <summary>
        /// Position of replaced text
        /// </summary>
        int OldPosition { get; }

        /// <summary>
        /// Text that was replaced
        /// </summary>
        string OldText { get; }
    }
}
