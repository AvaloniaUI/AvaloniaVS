using System.ComponentModel.Composition;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace AvaloniaVS.IntelliSense
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("xml")]
    [TagType(typeof(IErrorTag))]
    internal class XamlErrorTaggerProvider : ITaggerProvider
    {
        private readonly ITextStructureNavigatorSelectorService _navigatorProvider;
        private readonly ITableManagerProvider _tableManagerProvider;

        [ImportingConstructor]
        public XamlErrorTaggerProvider(
            ITextStructureNavigatorSelectorService navigatorProvider,
            ITableManagerProvider tableManagerProvider)
        {
            _navigatorProvider = navigatorProvider;
            _tableManagerProvider = tableManagerProvider;
        }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer.Properties.TryGetProperty<XamlErrorTagger>(
                typeof(XamlErrorTagger),
                out var existing))
            {
                return (ITagger<T>)existing;
            }

            if (buffer.Properties.TryGetProperty<PreviewerProcess>(
                typeof(PreviewerProcess),
                out var process))
            {
                var navigator = _navigatorProvider.GetTextStructureNavigator(buffer);
                var tagger = new XamlErrorTagger(_tableManagerProvider, buffer, navigator, process);
                buffer.Properties.AddProperty(typeof(XamlErrorTagger), tagger);
                tagger.Disposed += (s, e) => buffer.Properties.RemoveProperty(typeof(XamlErrorTagger));
                return (ITagger<T>)tagger;
            }

            return null;
        }
    }
}
