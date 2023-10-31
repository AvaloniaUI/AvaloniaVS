using System.ComponentModel.Composition;
using AvaloniaVS.Models;
using AvaloniaVS.Shared.IntelliSense;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace AvaloniaVS.IntelliSense
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("xml")]
    [Name("Avalonia XAML Completion")]
    internal class XamlCompletionSourceProvider : ICompletionSourceProvider
    {
        [ImportingConstructor]
        public XamlCompletionSourceProvider([Import] CompletionEngineSource completionEngineSource)
        {
            _completionEngineSource = completionEngineSource;
        }
        private readonly CompletionEngineSource _completionEngineSource;
        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            if (textBuffer.Properties.ContainsProperty(typeof(XamlBufferMetadata)))
            {
                return new XamlCompletionSource(textBuffer, _completionEngineSource);
            }

            return null;
        }
    }
}
