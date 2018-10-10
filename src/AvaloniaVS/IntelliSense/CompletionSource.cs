using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.Ide.CompletionEngine;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using CompletionSet = Microsoft.VisualStudio.Language.Intellisense.CompletionSet;
using VsCompletion = Microsoft.VisualStudio.Language.Intellisense.Completion;

namespace AvaloniaVS.IntelliSense
{
    internal class CompletionSource : ICompletionSource
    {
        private readonly ITextBuffer _textBuffer;
        private readonly CompletionSourceProvider _provider;
        private readonly CompletionEngine _engine = new CompletionEngine();

        public CompletionSource(ITextBuffer textBuffer, CompletionSourceProvider provider)
        {
            _textBuffer = textBuffer;
            _provider = provider;
        }

        public void Dispose()
        {
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            _textBuffer.Properties.TryGetProperty(typeof(Metadata), out Metadata metadata);
            if (metadata == null) {
                return;
            }

            var pos = session.TextView.Caret.Position.BufferPosition;
            var text = pos.Snapshot.GetText(0, pos.Position);
            var completions = _engine.GetCompletions(metadata, text, pos);

            if (completions != null && completions.Completions.Count != 0) {
                var curStart = completions.StartPosition;
                var span = new SnapshotSpan(pos.Snapshot, curStart, pos.Position - curStart);
                completionSets.Insert(0, new CompletionSet("Avalonia", "Avalonia",
                    pos.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive), completions.Completions
                        .Select(c => new VsCompletion(c.DisplayText, c.InsertText, c.Description, null, null)), null));
            }
        }
    }

    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("xml")]
    [Name("token completion")]
    internal class CompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new CompletionSource(textBuffer, this);
        }
    }
}
