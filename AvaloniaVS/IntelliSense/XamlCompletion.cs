using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Avalonia.Ide.CompletionEngine;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace AvaloniaVS.IntelliSense
{
    /// <summary>
    /// An Avalonia XAML intellisense completion suggestion.
    /// </summary>
    internal class XamlCompletion : Microsoft.VisualStudio.Language.Intellisense.Completion
    {
        public XamlCompletion(Completion completion, IVsImageService2 imageService)
            : base(
                completion.DisplayText,
                completion.InsertText,
                completion.Description,
                GetImage(completion.Kind, imageService),
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
            IEnumerable<Completion> source,
            IVsImageService2 imageService)
        {
            return source.Select(x => new XamlCompletion(x, imageService));
        }

        public static ImageSource GetImage(CompletionKind kind, IVsImageService2 imageService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var attributes = new ImageAttributes
            {
                StructSize = Marshal.SizeOf(typeof(ImageAttributes)),
                ImageType = (uint)_UIImageType.IT_Bitmap,
                Format = (uint)_UIDataFormat.DF_WPF,
                LogicalWidth = 16,
                LogicalHeight = 16,
                Flags = (uint)_ImageAttributesFlags.IAF_RequiredFlags,
            };

            var id = KnownMonikers.None;

            switch (kind)
            {
                case CompletionKind.AttachedProperty:
                case CompletionKind.Property:
                case CompletionKind.StaticProperty:
                    id = KnownMonikers.Property;
                    break;
                case CompletionKind.Class:
                    id = KnownMonikers.MarkupTag;
                    break;
                case CompletionKind.Enum:
                    id = KnownMonikers.EnumerationItemPublic;
                    break;
                case CompletionKind.MarkupExtension:
                case CompletionKind.Namespace:
                    id = KnownMonikers.Namespace;
                    break;
            }

            var image = imageService.GetImage(id, attributes);
            ErrorHandler.ThrowOnFailure(image.get_Data(out var data));
            return (ImageSource)data;
        }
    }
}
