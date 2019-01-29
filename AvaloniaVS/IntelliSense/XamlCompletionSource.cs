using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Ide.CompletionEngine;
using AvaloniaVS.Models;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Completion = Microsoft.VisualStudio.Language.Intellisense.Completion;
using CompletionSet = Microsoft.VisualStudio.Language.Intellisense.CompletionSet;

namespace AvaloniaVS.IntelliSense
{
    internal class XamlCompletionSource : ICompletionSource
    {
        private readonly ITextBuffer _buffer;
        private readonly CompletionEngine _engine;

        public XamlCompletionSource(ITextBuffer textBuffer)
        {
            _buffer = textBuffer;
            _engine = new CompletionEngine();
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (_buffer.Properties.TryGetProperty<XamlBufferMetadata>(typeof(XamlBufferMetadata), out var metadata) &&
                metadata.CompletionMetadata != null)
            {
                var pos = session.TextView.Caret.Position.BufferPosition;
                var text = pos.Snapshot.GetText(0, pos.Position);
                var completions = _engine.GetCompletions(metadata.CompletionMetadata, text, pos);

                if (completions?.Completions.Count > 0)
                {
                    var start = completions.StartPosition;
                    var span = new SnapshotSpan(pos.Snapshot, start, pos.Position - start);
                    var applicableTo = pos.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

                    completionSets.Insert(0, new CompletionSet(
                        "Avalonia",
                        "Avalonia",
                        applicableTo,
                        completions.Completions.Select(c => new Completion(c.DisplayText, c.InsertText, c.Description, null, null)),
                        null));
                }
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
