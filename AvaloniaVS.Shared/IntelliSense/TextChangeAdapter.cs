using Microsoft.VisualStudio.Text;
using AvaloniaTextChange = Avalonia.Ide.CompletionEngine.ITextChange;

namespace AvaloniaVS.IntelliSense
{
    public class TextChangeAdapter : AvaloniaTextChange
    {
        private readonly ITextChange _textChange;

        public TextChangeAdapter(ITextChange textChange)
        {
            _textChange = textChange;
        }

        /// <inheritdoc/>
        public int NewPosition => _textChange.NewPosition;

        /// <inheritdoc/>
        public string NewText => _textChange.NewText;

        /// <inheritdoc/>
        public int OldPosition => _textChange.OldPosition;

        /// <inheritdoc/>
        public string OldText => _textChange.OldText;
    }
}
