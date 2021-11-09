using CompletionMetadata = Avalonia.Ide.CompletionEngine.Metadata;

namespace AvaloniaVS.Models
{
    internal class XamlBufferMetadata
    {
        public CompletionMetadata CompletionMetadata { get; set; }

        public bool NeedInvalidation { get; set; } = true;
    }
}
