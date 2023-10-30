using System.ComponentModel.Composition;
using Avalonia.Ide.CompletionEngine;

namespace AvaloniaVS.Shared.IntelliSense
{
    [Export]
    public class CompletionEngineSource
    {
        public CompletionEngineSource()
        {
            CompletionEngine = new CompletionEngine();
        }
        public CompletionEngine CompletionEngine { get; }
    }
}
