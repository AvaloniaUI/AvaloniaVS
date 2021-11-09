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
        private static ImageSource[] s_images;

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

        private static ImageSource GetImage(CompletionKind kind, IVsImageService2 imageService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (s_images == null)
            {
                LoadImages(imageService);
            }

            return s_images[(int)kind];
        }

        private static void LoadImages(IVsImageService2 imageService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var capacity = Enum.GetValues(typeof(CompletionKind)).Cast<int>().Max() + 1;
            var attributes = new ImageAttributes
            {
                StructSize = Marshal.SizeOf(typeof(ImageAttributes)),
                ImageType = (uint)_UIImageType.IT_Bitmap,
                Format = (uint)_UIDataFormat.DF_WPF,
                LogicalWidth = 16,
                LogicalHeight = 16,
                Flags = (uint)_ImageAttributesFlags.IAF_RequiredFlags,
            };

            s_images = new ImageSource[capacity];
            s_images[(int)CompletionKind.Property] = LoadImage(imageService, KnownMonikers.Property, ref attributes);
            s_images[(int)CompletionKind.Event] = LoadImage(imageService, KnownMonikers.Event, ref attributes);
            s_images[(int)CompletionKind.Class] = LoadImage(imageService, KnownMonikers.MarkupTag, ref attributes);
            s_images[(int)CompletionKind.Enum] = LoadImage(imageService, KnownMonikers.EnumerationItemPublic, ref attributes);
            s_images[(int)CompletionKind.Namespace] = LoadImage(imageService, KnownMonikers.Namespace, ref attributes);

            s_images[(int)CompletionKind.AttachedEvent] = s_images[(int)CompletionKind.Event];
            s_images[(int)CompletionKind.AttachedProperty] = s_images[(int)CompletionKind.Property];
            s_images[(int)CompletionKind.StaticProperty] = s_images[(int)CompletionKind.Property];
            s_images[(int)CompletionKind.MarkupExtension] = s_images[(int)CompletionKind.Namespace];
        }

        private static ImageSource LoadImage(
            IVsImageService2 imageService,
            ImageMoniker moniker,
            ref ImageAttributes attributes)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var image = imageService.GetImage(moniker, attributes);
            ErrorHandler.ThrowOnFailure(image.get_Data(out var data));
            return (ImageSource)data;
        }
    }
}
