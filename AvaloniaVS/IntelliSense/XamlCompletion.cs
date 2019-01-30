using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Ide.CompletionEngine;

namespace AvaloniaVS.IntelliSense
{
    /// <summary>
    /// An Avalonia XAML intellisense completion suggestion.
    /// </summary>
    internal class XamlCompletion : Microsoft.VisualStudio.Language.Intellisense.Completion
    {
        public XamlCompletion(Completion completion)
            : base(
            completion.DisplayText,
            completion.InsertText,
            completion.Description,
            null,
            null)
        {
            if (completion.RecommendedCursorOffset.HasValue)
            {
                CursorOffset = completion.InsertText.Length - completion.RecommendedCursorOffset.Value;
            }

            IsClass = completion.Kind == CompletionKind.Class;
        }

        public int CursorOffset { get; }
        public bool IsClass { get; }

        public static IEnumerable<XamlCompletion> Create(IEnumerable<Completion> source)
        {
            return source.Select(x => new XamlCompletion(x));
        }
    }
}
