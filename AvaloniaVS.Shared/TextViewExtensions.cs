using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace AvaloniaVS;

internal static class TextViewExtensions
{
    public static NormalizedSnapshotSpanCollection GetSpanInView(this ITextView textView, SnapshotSpan span)
        => textView.BufferGraph.MapUpToSnapshot(span, SpanTrackingMode.EdgeInclusive, textView.TextSnapshot);

    public static void SetSelection(
        this ITextView textView, VirtualSnapshotPoint anchorPoint, VirtualSnapshotPoint activePoint)
    {
        var isReversed = activePoint < anchorPoint;
        var start = isReversed ? activePoint : anchorPoint;
        var end = isReversed ? anchorPoint : activePoint;
        SetSelection(textView, new SnapshotSpan(start.Position, end.Position), isReversed);
    }

    public static void SetSelection(
        this ITextView textView, SnapshotSpan span, bool isReversed = false)
    {
        var spanInView = textView.GetSpanInView(span).Single();
        textView.Selection.Select(spanInView, isReversed);
        textView.Caret.MoveTo(isReversed ? spanInView.Start : spanInView.End);
    }
}
