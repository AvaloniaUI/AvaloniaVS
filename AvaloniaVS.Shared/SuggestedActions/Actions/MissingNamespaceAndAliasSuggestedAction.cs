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
    internal class MissingNamespaceAndAliasSuggestedAction : BaseSuggestedAction, ISuggestedAction
    {
        private readonly ITrackingSpan _span;
        private readonly ITextSnapshot _snapshot;
        private readonly string _namespaceAlias;
        private readonly string _targetClassName;
        private readonly KeyValuePair<string, string> _targetClassMetadata;
        private readonly IWpfDifferenceViewerFactoryService _diffFactory;
        private readonly IDifferenceBufferFactoryService _diffBufferFactory;
        private readonly ITextBufferFactoryService _bufferFactory;
        private readonly Dictionary<string, string> _aliases;
        private readonly ITextViewRoleSet _previewRoleSet;

        public MissingNamespaceAndAliasSuggestedAction(ITrackingSpan span,
            IWpfDifferenceViewerFactoryService diffFactory,
            IDifferenceBufferFactoryService diffBufferFactory,
            ITextBufferFactoryService bufferFactory,
            ITextEditorFactoryService textEditorFactoryService,
            IReadOnlyDictionary<string, ISet<string>> inverseNamespaces,
            Dictionary<string, string> aliases)
        {
            _span = span;
            _snapshot = _span.TextBuffer.CurrentSnapshot;
            _targetClassName = _span.GetText(_snapshot);
            _targetClassMetadata = inverseNamespaces
                .Where(x => x.Key.Split('.').Last() == _targetClassName)
                .Select(x => new KeyValuePair<string, string>(x.Key, x.Value.First()))
                .FirstOrDefault();

            // _targetClassMetadata.Value is the namespace of the control we are trying to add the namespace to. 
            // It is usually in the format using:MyNamespace.Something.
            // So to get the prefix for the control we are splitting it by ':'
            // Then taking the MyNamespace.Something part and splitting it by '.' and getting Something.
            _namespaceAlias = _targetClassMetadata.Value.Split(':').Last().Split('.').Last();
            DisplayText = $"Add xmlns {_namespaceAlias}";
            _diffFactory = diffFactory;
            _diffBufferFactory = diffBufferFactory;
            _bufferFactory = bufferFactory;
            _aliases = aliases;
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

            buffer.Replace(_span.GetSpan(_snapshot), $"{_namespaceAlias.ToLower()}:{_targetClassName}");

            // We get the index of the last namespace in the list and add the last namespace length without quotes and add 2.
            // One for qutation mark and one to place the new namespace in an empty space.
            buffer.Insert(buffer.CurrentSnapshot.GetText().IndexOf(lastNs) + lastNs.Length + 2, $"xmlns:{_namespaceAlias.ToLower()}=\"{_targetClassMetadata.Value}\"");
        }

    }
}
