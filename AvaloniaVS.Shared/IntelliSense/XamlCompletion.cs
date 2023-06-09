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
                completion.Kind.ToString(),
                suffix:  string.IsNullOrWhiteSpace(completion.Suffix) ? string.Empty : $"({completion.Suffix})")
        {
            if (completion.RecommendedCursorOffset.HasValue)
            {
                CursorOffset = completion.InsertText.Length - completion.RecommendedCursorOffset.Value;
            }

            Kind = completion.Kind;
            DeleteTextOffset = completion.DeleteTextOffset;
        }

        public int? DeleteTextOffset { get; }

        public override string InsertionText
        {
            get
            {
                if (HasFlag(Kind, CompletionKind.Name) && !string.IsNullOrEmpty(Suffix))
                {
                    return $"{Suffix.Substring(1,Suffix.Length-2)}#{base.InsertionText}";
                }
                return base.InsertionText;
            }

            set => base.InsertionText = value;
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
            else if (HasFlag(kind, CompletionKind.Selector))
            {
                return s_images[(int)CompletionKind.Enum];
            }
            else if (HasFlag(kind, CompletionKind.Name))
            {
                return s_images[(int)CompletionKind.Class];
            }
            else if (HasFlag(kind, CompletionKind.Comment))
            {
                return s_images[(int)CompletionKind.Comment];
            }
            return s_images[(int)kind];
        }

        private static bool HasFlag(CompletionKind test, CompletionKind expected)
        {
            return (test & expected) == expected;
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
            s_images[(int)CompletionKind.Selector] = KnownMonikers.Namespace;
            s_images[(int)CompletionKind.Comment] = KnownMonikers.XMLCommentTag;
        }
    }
}
