using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace AvaloniaVS.Shared.SuggestedActions
{
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("SuggestedActionsSourceProvider")]
    [ContentType("xml")]
    internal class SuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        private readonly IWpfDifferenceViewerFactoryService _diffFactory;
        private readonly IDifferenceBufferFactoryService _diffBufferFactory;
        private readonly ITextBufferFactoryService _bufferFactory;
        private readonly ITextEditorFactoryService _textEditorFactoryService;

        [ImportingConstructor]
        public SuggestedActionsSourceProvider([Import] IWpfDifferenceViewerFactoryService diffFactory, [Import] IDifferenceBufferFactoryService diffBufferFactory,
            [Import] ITextBufferFactoryService bufferFactory, [Import] ITextEditorFactoryService textEditorFactoryService)
        {
            _diffFactory = diffFactory;
            _diffBufferFactory = diffBufferFactory;
            _bufferFactory = bufferFactory;
            _textEditorFactoryService = textEditorFactoryService;
        }

        [Import(typeof(ITextStructureNavigatorSelectorService))]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            if (textBuffer == null && textView == null)
            {
                return null;
            }
            return new SuggestedActionsSource(this, textView, textBuffer, _diffFactory, _diffBufferFactory,
                _bufferFactory, _textEditorFactoryService);
        }
    }
}
