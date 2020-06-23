using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Ide.CompletionEngine;
using AvaloniaVS.Models;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Serilog;

namespace AvaloniaVS.IntelliSense
{
    internal class XamlTextManipulatorRegistrar
    {
        private readonly IWpfTextView _textView;
        private readonly ITextBuffer _buffer;
        private bool _isChangingText = false;

        public XamlTextManipulatorRegistrar(IWpfTextView textView)
        {
            _textView = textView;
            _buffer = textView.TextBuffer;

            _textView.Closed += TextView_Closed;
            _buffer.Changed += TextBuffer_Changed;
        }

        private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            if (_isChangingText)
            {
                return;
            }

            try
            {
                if (_buffer.Properties.TryGetProperty<XamlBufferMetadata>(typeof(XamlBufferMetadata), out var metadata) &&
                metadata.CompletionMetadata != null)
                {
                    var sw = Stopwatch.StartNew();
                    var pos = _textView.Caret.Position.BufferPosition;
                    var text = _buffer.CurrentSnapshot.GetText();


                    foreach (Microsoft.VisualStudio.Text.ITextChange change in e.Changes.ToList())
                    {
                        var textManipulator = new TextManipulator(text, change.NewPosition);
                        var avaloniaChange = new TextChangeAdapter(change);
                        var manipulations = textManipulator.ManipulateText(avaloniaChange);
                        if (manipulations?.Count > 0)
                        {
                            _isChangingText = true;
                            ApplyManipulations(manipulations);
                            Log.Logger.Verbose("XAML manipulation took {Time}", sw.Elapsed);
                        }
                    }
                    sw.Stop();
                }
            }
            finally
            {
                _isChangingText = false;
            }
        }

        private void ApplyManipulations(IList<TextManipulation> manipulations)
        {
            var edit = _buffer.CreateEdit();
            foreach (var manipulation in manipulations)
            {
                switch (manipulation.Type)
                {
                    case ManipulationType.Insert:
                        edit.Insert(manipulation.Start, manipulation.Text);
                        break;
                    case ManipulationType.Delete:
                        edit.Delete(Span.FromBounds(manipulation.Start, manipulation.End));
                        break;
                }

            }
            edit.Apply();
        }

        private void TextView_Closed(object sender, EventArgs e)
        {
            if (_textView != null)
            {
                _textView.Closed -= TextView_Closed;
                _buffer.Changed -= TextBuffer_Changed;
            }
        }

    }
}
