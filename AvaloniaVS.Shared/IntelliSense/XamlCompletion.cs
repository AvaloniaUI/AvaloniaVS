using System.Collections.Generic;
using System.Linq;
using Avalonia.Ide.CompletionEngine;
using static AvaloniaVS.Utils.ImageMonikerUtility;

namespace AvaloniaVS.IntelliSense
{
    /// <summary>
    /// An Avalonia XAML intellisense completion suggestion.
    /// </summary>
    internal class XamlCompletion : Microsoft.VisualStudio.Language.Intellisense.Completion4
    {
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
    }
}
