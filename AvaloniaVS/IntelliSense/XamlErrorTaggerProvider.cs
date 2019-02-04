using System;
using System.ComponentModel.Composition;
using AvaloniaVS.Views;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace AvaloniaVS.IntelliSense
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("xml")]
    [TagType(typeof(ErrorTag))]
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
            if (buffer.Properties.TryGetProperty<DesignerPane>(
                typeof(DesignerPane),
                out var pane))
            {
                var navigator = _navigatorProvider.GetTextStructureNavigator(buffer);
                return (ITagger<T>)new XamlErrorTagger(_tableManagerProvider, buffer, navigator, pane);
            }

            return null;
        }
    }
}
