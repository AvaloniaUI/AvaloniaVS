using System;
using System.Windows;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;

namespace AvaloniaVS.Shared.SuggestedActions.Helpers
{
    internal static class PreviewProvider
    {
        public static FrameworkElement GetPreview(ITextBufferFactoryService bufferFactory, ITrackingSpan span, IDifferenceBufferFactoryService diffBufferFactory, IWpfDifferenceViewerFactoryService diffFactory, ITextViewRoleSet previewRoleSet, Action<ITextBuffer> applyNamespaceSuggestionAction)
        {
            var snapshot = span.TextBuffer.CurrentSnapshot;

            var leftBuffer = bufferFactory.CreateTextBuffer(snapshot.GetText(), span.TextBuffer.ContentType);

            var rightBuffer = bufferFactory.CreateTextBuffer(snapshot.GetText(), span.TextBuffer.ContentType);

            applyNamespaceSuggestionAction(rightBuffer);

            var diffBuffer = diffBufferFactory.CreateDifferenceBuffer(leftBuffer, rightBuffer);
            var diffView = diffFactory.CreateDifferenceView(diffBuffer, previewRoleSet);
            diffView.ViewMode = DifferenceViewMode.Inline;
            diffView.InlineView.VisualElement.Focusable = false;

            // DiffView size to content
            diffView.DifferenceBuffer.SnapshotDifferenceChanged += (sender, args) =>
            {
                diffView.InlineView.DisplayTextLineContainingBufferPosition(
                    new SnapshotPoint(diffView.DifferenceBuffer.CurrentInlineBufferSnapshot, 0),
                    0.0, ViewRelativePosition.Top, double.MaxValue, double.MaxValue
                );

                var width = Math.Max(diffView.InlineView.MaxTextRightCoordinate * (diffView.InlineView.ZoomLevel / 100), 400); // Width of the widest line.
                var height = diffView.InlineView.LineHeight * (diffView.InlineView.ZoomLevel / 100) * // Height of each line.
                    diffView.DifferenceBuffer.CurrentInlineBufferSnapshot.LineCount;

                diffView.VisualElement.Width = width;
                diffView.VisualElement.Height = height;
            };
            return diffView.VisualElement;
        }

    }
}
