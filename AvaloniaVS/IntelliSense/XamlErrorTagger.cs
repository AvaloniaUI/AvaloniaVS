using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Remote.Protocol.Designer;
using AvaloniaVS.Services;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
        private ITagSpan<IErrorTag> _tagSpan;
        private ITableDataSink _sink;

        public XamlErrorTagger(
            ITableManagerProvider tableManagerProvider,
            ITextBuffer buffer,
            ITextStructureNavigator navigator,
            PreviewerProcess process)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _buffer = buffer;
            _navigator = navigator;
            _process = process;
            _process.ErrorChanged += HandleErrorChanged;
            _error = process.Error;

            // Get the document path and containing project name.
            var document = GetDocument(buffer);
            _path = document?.FilePath;
            _projectName = GetProject(_path)?.Name;

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
            var result = GetErrorTag(spans);

            if (result != null)
            {
                _tagSpan = result;
            }

            return result != null ? new[] { result } : Array.Empty<ITagSpan<IErrorTag>>();
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

        private TagSpan<IErrorTag> GetErrorTag(NormalizedSnapshotSpanCollection spans)
        {
            if (_error?.LineNumber == null)
            {
                return null;
            }

            var line = _error.LineNumber.Value - 1;
            var col = (_error?.LinePosition ?? 1) - 1;

            if (line < 0 || line >= _buffer.CurrentSnapshot.LineCount || col < 0)
            {
                return null;
            }

            var snapshotline = _buffer.CurrentSnapshot.GetLineFromLineNumber(line);

            if (snapshotline.Start.Position + col >= snapshotline.Snapshot.Length)
            {
                return null;
            }

            var start = snapshotline.Start + col;
            var startSpan = new SnapshotSpan(start, start + 1);
            var span = _navigator.GetSpanOfFirstChild(startSpan);
            var tag = new ErrorTag(PredefinedErrorTypeNames.CompilerError, _error.Message);

            if (!spans.IntersectsWith(span))
            {
                return null;
            }

            return new TagSpan<IErrorTag>(span, tag);
        }

        private void HandleErrorChanged(object sender, EventArgs e)
        {
            if (_tagSpan != null)
            {
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(_tagSpan.Span));
                _tagSpan = null;
            }

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

        private static ITextDocument GetDocument(ITextBuffer buffer)
        {
            buffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var document);
            return document;
        }

        private static Project GetProject(string fileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                return null;
            }

            var dte2 = (DTE2)Package.GetGlobalService(typeof(SDTE));
            var projItem = dte2?.Solution.FindProjectItem(fileName);
            return projItem?.ContainingProject;
        }
    }
}
