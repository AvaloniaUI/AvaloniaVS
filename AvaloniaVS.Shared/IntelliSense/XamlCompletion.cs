using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Ide.CompletionEngine;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;

namespace AvaloniaVS.IntelliSense
{
    /// <summary>
    /// An Avalonia XAML intellisense completion suggestion.
    /// </summary>
    internal class XamlCompletion : Microsoft.VisualStudio.Language.Intellisense.Completion4
    {
        private static ImageMoniker[] s_images;

        public XamlCompletion(Completion completion)
            : base(
                completion.DisplayText,
                completion.InsertText,
                completion.Description,
                GetImage(completion.Kind),
                completion.Kind.ToString())
        {
            if (completion.RecommendedCursorOffset.HasValue)
            {
                CursorOffset = completion.InsertText.Length - completion.RecommendedCursorOffset.Value;
            }

            Kind = completion.Kind;
        }

        public int CursorOffset { get; }

        public CompletionKind Kind { get; }

        public static IEnumerable<XamlCompletion> Create(
            IEnumerable<Completion> source)
        {
            return source.Select(x => new XamlCompletion(x));
        }

        private static ImageMoniker GetImage(CompletionKind kind)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (s_images == null)
            {
                LoadImages();
            }

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

        private static void LoadImages()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var capacity = Enum.GetValues(typeof(CompletionKind)).Cast<int>().Max() + 1;
            
            s_images = new ImageMoniker[capacity];
            s_images[(int)CompletionKind.Property] = KnownMonikers.Property;
            s_images[(int)CompletionKind.Event] = KnownMonikers.Event;
            s_images[(int)CompletionKind.Class] = KnownMonikers.METATag;
            s_images[(int)CompletionKind.Enum] = KnownMonikers.EnumerationItemPublic;
            s_images[(int)CompletionKind.Namespace] = KnownMonikers.Namespace;

            s_images[(int)CompletionKind.AttachedEvent] = KnownMonikers.Event;
            s_images[(int)CompletionKind.AttachedProperty] = KnownMonikers.Property;
            s_images[(int)CompletionKind.StaticProperty] = KnownMonikers.EnumerationItemPublic;
            s_images[(int)CompletionKind.MarkupExtension] = KnownMonikers.Namespace;
            s_images[(int)CompletionKind.DataProperty] = KnownMonikers.DatabaseProperty;
            s_images[(int)CompletionKind.TargetTypeClass] = KnownMonikers.ClassPublic;
        }
    }
}
