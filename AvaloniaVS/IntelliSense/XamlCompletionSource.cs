using System;
using System.Collections.Generic;
using System.Diagnostics;
using AvaloniaVS.Models;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Serilog;
using CompletionEngine = Avalonia.Ide.CompletionEngine.CompletionEngine;

namespace AvaloniaVS.IntelliSense
{
    internal class XamlCompletionSource : ICompletionSource
    {
        private readonly ITextBuffer _buffer;
        private readonly IVsImageService2 _imageService;
        private readonly CompletionEngine _engine;

        public XamlCompletionSource(ITextBuffer textBuffer, IVsImageService2 imageService)
        {
            _buffer = textBuffer;
            _imageService = imageService;
            _engine = new CompletionEngine();
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (_buffer.Properties.TryGetProperty<XamlBufferMetadata>(typeof(XamlBufferMetadata), out var metadata) &&
                metadata.CompletionMetadata != null)
            {
                var sw = Stopwatch.StartNew();
                var pos = session.TextView.Caret.Position.BufferPosition;
                var text = pos.Snapshot.GetText(0, pos.Position);
                _buffer.Properties.TryGetProperty("AssemblyName", out string assemblyName);
                var completions = _engine.GetCompletions(metadata.CompletionMetadata, text, pos, assemblyName);

                if (completions?.Completions.Count > 0)
                {
                    var start = completions.StartPosition;
                    var span = new SnapshotSpan(pos.Snapshot, start, pos.Position - start);
                    var applicableTo = pos.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

                    completionSets.Insert(0, new CompletionSet(
                        "Avalonia",
                        "Avalonia",
                        applicableTo,
                        XamlCompletion.Create(completions.Completions, _imageService),
                        null));

                    Log.Logger.Verbose("XAML completion took {Time}", sw.Elapsed);
                }

                sw.Stop();
            }
        }

        public void Dispose()
        {
        }
    }
}
