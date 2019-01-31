using System;
using System.ComponentModel.Composition;
using AvaloniaVS.Models;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace AvaloniaVS.IntelliSense
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("xml")]
    [Name("Avalonia XAML Completion")]
    internal class XamlCompletionSourceProvider : ICompletionSourceProvider
    {
        private readonly IVsImageService2 _imageService;

        [ImportingConstructor]
        public XamlCompletionSourceProvider(
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
        {
            _imageService = serviceProvider.GetService<IVsImageService2, SVsImageService>();
        }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            if (textBuffer.Properties.ContainsProperty(typeof(XamlBufferMetadata)))
            {
                return new XamlCompletionSource(textBuffer, _imageService);
            }

            return null;
        }
    }
}
