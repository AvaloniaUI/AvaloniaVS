using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AvaloniaVS.Models;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Serilog;
using CompletionEngine = Avalonia.Ide.CompletionEngine.CompletionEngine;

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
                var sw = Stopwatch.StartNew();
                var pos = session.TextView.Caret.Position.BufferPosition;
                var text = pos.Snapshot.GetText();
                _buffer.Properties.TryGetProperty("AssemblyName", out string assemblyName);
                var completions = _engine.GetCompletions(metadata.CompletionMetadata, text, pos, assemblyName);

                if (completions?.Completions.Count > 0)
                {
                    var start = completions.StartPosition;

                    // TODO: this should be handled in the completion engine
                    // pseudoclasses should only be returned in a Selector, so this is an easy filter
                    // We need to offset the start though for pseudoclasses to remove what they're 
                    // attached to: Control:pointerover -> :pointerover
                    if (completions.Completions[0].DisplayText.StartsWith(":"))
                    {
                        for (int i = pos - 1; i >= 0; i--)
                        {
                            if (char.IsWhiteSpace(text[i]) || text[i] == ':')
                            {
                                start = i;
                                break;
                            }
                        }
                    }

                    var span = new SnapshotSpan(pos.Snapshot, start, pos.Position - start);
                    var applicableTo = pos.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

                    completionSets.Insert(0, new CompletionSet(
                        "Avalonia",
                        "Avalonia",
                        applicableTo,
                        XamlCompletion.Create(completions.Completions),
                        null));

                    // This selects the first item in the completion popup - otherwise you have to physically
                    // interact with the completion list (either via mouse or keyboard arrows) otherwise tab
                    // or space won't trigger it
                    // TODO: We should really try to find the best match of the completion list and select that
                    // instead, but that's more than I want to do right now
                    if (completions.Completions.Count > 0)
                    {
                        completionSets[0].SelectionStatus = new CompletionSelectionStatus(
                            completionSets[0].Completions[0], true, false);
                    }

                    string completionHint = completions.Completions.Count == 0 ?
                        "no completions found" :
                        $"{completions.Completions.Count} completions found (First:{completions.Completions.FirstOrDefault()?.DisplayText})";

                    Log.Logger.Verbose("XAML completion took {Time}, {CompletionHint}", sw.Elapsed, completionHint);
                }

                sw.Stop();
            }
        }

        public void Dispose()
        {
        }
    }
}
