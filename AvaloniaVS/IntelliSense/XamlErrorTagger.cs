using System;
using System.Collections.Generic;
using Avalonia.Remote.Protocol.Designer;
using AvaloniaVS.Services;
using AvaloniaVS.Views;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;

namespace AvaloniaVS.IntelliSense
{
    internal class XamlErrorTagger : ITagger<IErrorTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly ITextStructureNavigator _navigator;
        private DesignerPane _pane;
        private PreviewerProcess _process;
        private ExceptionDetails _error;

        public XamlErrorTagger(
            ITextBuffer buffer,
            ITextStructureNavigator navigator,
            DesignerPane pane)
        {
            _buffer = buffer;
            _navigator = navigator;
            _pane = pane;

            if (pane.Process != null)
            {
                _process = pane.Process;
                _process.ErrorChanged += HandleErrorChanged;
            }
            else
            {
                pane.Initialized += PaneInitialized;
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void Dispose()
        {
            _process.ErrorChanged -= HandleErrorChanged;
            _pane.Initialized -= PaneInitialized;
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_error?.LineNumber != null)
            {
                var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(_error.LineNumber.Value - 1);
                var start = line.Start + ((_error?.LinePosition ?? 1) - 1);
                var startSpan = new SnapshotSpan(start, start + 1);
                var span = _navigator.GetSpanOfFirstChild(startSpan);
                var tag = new ErrorTag(PredefinedErrorTypeNames.CompilerError, _error.Message);
                return new[] { new TagSpan<IErrorTag>(span, tag) };
            }

            return Array.Empty<ITagSpan<IErrorTag>>();
        }

        private void HandleErrorChanged(object sender, EventArgs e)
        {
            RaiseTagsChanged(_error);
            _error = _process.Error;
            RaiseTagsChanged(_error);
        }

        private void RaiseTagsChanged(ExceptionDetails error)
        {
            if (error?.LineNumber != null &&
                TagsChanged != null &&
                error.LineNumber.Value < _buffer.CurrentSnapshot.LineCount)
            {
                var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(error.LineNumber.Value - 1);
                TagsChanged(this, new SnapshotSpanEventArgs(line.Extent));
            }
        }

        private void PaneInitialized(object sender, EventArgs e)
        {
            _process = _pane.Process;
            _process.ErrorChanged += HandleErrorChanged;
            RaiseTagsChanged(_process.Error);
            _pane.Initialized -= PaneInitialized;
        }
    }
}
