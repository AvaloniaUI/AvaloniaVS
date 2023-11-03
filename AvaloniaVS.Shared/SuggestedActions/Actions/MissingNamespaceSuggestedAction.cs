using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaVS.Shared.SuggestedActions.Actions.Base;
using AvaloniaVS.Shared.SuggestedActions.Helpers;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;

namespace AvaloniaVS.Shared.SuggestedActions.Actions
{
    internal class MissingNamespaceSuggestedAction : BaseSuggestedAction, ISuggestedAction
    {
        private readonly ITrackingSpan _span;
        private readonly KeyValuePair<string, string> _targetClassMetadata;
        private readonly IWpfDifferenceViewerFactoryService _diffFactory;
        private readonly IDifferenceBufferFactoryService _diffBufferFactory;
        private readonly ITextBufferFactoryService _bufferFactory;
        private readonly Dictionary<string, string> _aliases;
        private readonly string _alias;
        private readonly ITextViewRoleSet _previewRoleSet;

        public MissingNamespaceSuggestedAction(ITrackingSpan span, IWpfDifferenceViewerFactoryService diffFactory, IDifferenceBufferFactoryService diffBufferFactory,
            ITextBufferFactoryService bufferFactory, ITextEditorFactoryService textEditorFactoryService, IReadOnlyDictionary<string, string> inverseNamespaces,
            Dictionary<string, string> aliases, string alias)
        {
            _span = span;
            _targetClassMetadata = inverseNamespaces.FirstOrDefault(x => x.Key.Split('.').Last() == _span.GetText(_span.TextBuffer.CurrentSnapshot));
            DisplayText = $"Add xmlns {alias}";
            _diffFactory = diffFactory;
            _diffBufferFactory = diffBufferFactory;
            _bufferFactory = bufferFactory;
            _aliases = aliases;
            _alias = alias;
            _previewRoleSet = textEditorFactoryService.CreateTextViewRoleSet(PredefinedTextViewRoles.Analyzable);
        }

        public string DisplayText { get; }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<object>(PreviewProvider.GetPreview(_bufferFactory, _span, _diffBufferFactory, _diffFactory, _previewRoleSet, ApplySuggestion));
        }

        public void Invoke(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            ApplySuggestion(_span.TextBuffer);
        }

        private void ApplySuggestion(ITextBuffer buffer)
        {
            var lastNs = _aliases.Last().Value;

            buffer.Insert(buffer.CurrentSnapshot.GetText().IndexOf(lastNs) + lastNs.Length + 2, $"xmlns:{_alias}=\"{_targetClassMetadata.Value}\"");
        }

    }
}
