using System;
using System.Collections.Generic;
using Avalonia.Remote.Protocol.Designer;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;

namespace AvaloniaVS.IntelliSense
{
    internal class XamlErrorTagger : ITagger<IErrorTag>, ITableDataSource, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly ITextStructureNavigator _navigator;
        private readonly string _projectName;
        private readonly string _path;
        private PreviewerProcess _process;
        private ExceptionDetails _error;
        private ITableDataSink _sink;

        public XamlErrorTagger(
            ITableManagerProvider tableManagerProvider,
            ITextBuffer buffer,
            ITextStructureNavigator navigator,
            PreviewerProcess process)
        {
            _buffer = buffer;
            _navigator = navigator;
            _process = process;
            _process.ErrorChanged += HandleErrorChanged;
            _error = process.Error;

            // Get the document path and containing project name.
            var document = buffer.GetDocument();
            _path = document?.FilePath;
            _projectName = document?.GetProject()?.Name;

            // Register ourselves with the error list.
            var tableManager = tableManagerProvider.GetTableManager(StandardTables.ErrorsTable);
            tableManager.AddSource(this,
                StandardTableColumnDefinitions.Column,
                StandardTableColumnDefinitions.DocumentName,
                StandardTableColumnDefinitions.ErrorSeverity,
                StandardTableColumnDefinitions.Line,
                StandardTableColumnDefinitions.Text);
        }

        string ITableDataSource.SourceTypeIdentifier => StandardTableDataSources.ErrorTableDataSource;
        string ITableDataSource.Identifier => "Avalonia XAML designer errors";
        string ITableDataSource.DisplayName => "Avalonia XAML";

        public event EventHandler Disposed;
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void Dispose()
        {
            _sink?.RemoveAllEntries();

            if (_process != null)
            {
                _process.ErrorChanged -= HandleErrorChanged;
            }

            Disposed?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_error?.LineNumber != null)
            {
                var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(Math.Max(_error.LineNumber.Value - 1, 0));
                var start = line.Start + Math.Max(((_error?.LinePosition ?? 1) - 1), 0);
                var startSpan = new SnapshotSpan(start, start + 1);
                var span = _navigator.GetSpanOfFirstChild(startSpan);
                var tag = new ErrorTag(PredefinedErrorTypeNames.CompilerError, _error.Message);

                if (spans.OverlapsWith(span))
                {
                    return new[] { new TagSpan<IErrorTag>(span, tag) };
                }
            }

            return Array.Empty<ITagSpan<IErrorTag>>();
        }

        IDisposable ITableDataSource.Subscribe(ITableDataSink sink)
        {
            _sink = sink;
            
            if (_error != null)
            {
                _sink.AddEntries(new[] { new XamlErrorTableEntry(_projectName, _path, _error) });
            }

            return null;
        }

        private void HandleErrorChanged(object sender, EventArgs e)
        {
            RaiseTagsChanged(_error);

            _error = _process.Error;

            if (_sink != null)
            {
                if (_error != null)
                {
                    _sink.AddEntries(new[] { new XamlErrorTableEntry(_projectName, _path, _error) }, true);
                }
                else
                {
                    _sink.RemoveAllEntries();
                }
            }

            RaiseTagsChanged(_error);
        }

        private void RaiseTagsChanged(ExceptionDetails error)
        {
            if (error?.LineNumber != null &&
                TagsChanged != null &&
                error.LineNumber.Value < _buffer.CurrentSnapshot.LineCount)
            {
                var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(Math.Max(error.LineNumber.Value - 1, 0));
                TagsChanged(this, new SnapshotSpanEventArgs(line.Extent));
            }
        }
    }
}
