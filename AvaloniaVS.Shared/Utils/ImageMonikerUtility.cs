#nullable enable
using System;
using System.Linq;
using Avalonia.Ide.CompletionEngine;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;

namespace AvaloniaVS.Utils;

internal class ImageMonikerUtility
{
    private static ImageMoniker[]? s_images;

    public static ImageMoniker GetImage(CompletionKind kind)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        s_images ??= LoadImages();

        if (HasFlag(kind, CompletionKind.DataProperty))
        {
            return s_images[(int)CompletionKind.DataProperty];
        }
        else if (HasFlag(kind, CompletionKind.TargetTypeClass))
        {
            return s_images[(int)CompletionKind.TargetTypeClass];
        }
        else if (HasFlag(kind, CompletionKind.VS_XMLNS))
        {
            return s_images[(int)CompletionKind.Enum];
        }

        return s_images[(int)kind];

        bool HasFlag(CompletionKind test, CompletionKind expected)
        {
            return (test & expected) == expected;
        }
    }

    private static ImageMoniker[] LoadImages()
    {
        var capacity = Enum.GetValues(typeof(CompletionKind)).Cast<int>().Max() + 1;

        var images = new ImageMoniker[capacity];
        images[(int)CompletionKind.Property] = KnownMonikers.Property;
        images[(int)CompletionKind.Event] = KnownMonikers.Event;
        images[(int)CompletionKind.Class] = KnownMonikers.METATag;
        images[(int)CompletionKind.Enum] = KnownMonikers.EnumerationItemPublic;
        images[(int)CompletionKind.Namespace] = KnownMonikers.Namespace;

        images[(int)CompletionKind.AttachedEvent] = KnownMonikers.Event;
        images[(int)CompletionKind.AttachedProperty] = KnownMonikers.Property;
        images[(int)CompletionKind.StaticProperty] = KnownMonikers.EnumerationItemPublic;
        images[(int)CompletionKind.MarkupExtension] = KnownMonikers.Namespace;
        images[(int)CompletionKind.DataProperty] = KnownMonikers.DatabaseProperty;
        images[(int)CompletionKind.TargetTypeClass] = KnownMonikers.ClassPublic;
        return images;
    }
}
