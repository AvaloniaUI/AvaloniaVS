using System;
using System.Collections.Generic;

namespace Avalonia.Ide.CompletionEngine
{
    public static class ITextChangeExtensions
    {
        public static IEnumerable<TextManipulation> AsManipulations(this ITextChange textChange, int offset = 0)
        {
            if (!string.IsNullOrEmpty(textChange.OldText))
            {
                yield return TextManipulation.Delete(textChange.OldPosition + offset + 1, textChange.OldText.Length);
            }

            if (!string.IsNullOrEmpty(textChange.NewText))
            {
                yield return TextManipulation.Insert(textChange.NewPosition + offset + 1, textChange.NewText);
            }
        }

        /// <summary>
        /// TextChange reversal is required to compare original text (before change)
        /// </summary>
        /// <param name="textChangeOffset">
        /// As text change is scoped in document this param allows to apply it to short strings, ie. tag names
        /// </param>
        public static string ReverseOn(this ITextChange textChange, string input, int textChangeOffset = 0)
        {
            if (!string.IsNullOrEmpty(textChange.NewText))
            {
                var start = textChange.NewPosition - textChangeOffset;
                input = input.Remove(start, Math.Min(input.Length - start,textChange.NewText.Length));
            }
            if (!string.IsNullOrEmpty(textChange.OldText))
            {
                input = input.Insert(textChange.OldPosition - textChangeOffset, textChange.OldText);
            }
            return input;
        }
    }
}
