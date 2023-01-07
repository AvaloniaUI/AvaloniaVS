using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Ide.CompletionEngine;
using Xunit;

namespace CompletionEngineTests.Manipulator
{
    public class ManipulatorTestBase
    {
        /// <summary>
        /// Asserts that after user writes text it will be replaced
        /// </summary>
        /// <param name="baseText">Text, $ tag marks cursor </param>
        /// <param name="userInput">Text to input at cursor ($)</param>
        /// <param name="expectedOutput">Final text with replacements</param>
        public void AssertInsertion(string baseText, string userInput, string expectedOutput)
        {
            (int cursor, string text) = TestUtils.PrepareTextWithCursor(baseText);
            var inputText = baseText.Replace("$", userInput);

            TextManipulator manipulator = new TextManipulator(inputText, cursor);

            var change = new TextChange(cursor, userInput, cursor, "");
            var manipulations = manipulator.ManipulateText(change);
            var actualOutput = ApplyManipulations(inputText, manipulations);

            Assert.Equal(expectedOutput, actualOutput);
        }

        /// <summary>
        /// Asserts that after user writes text it will be replaced
        /// </summary>
        /// <param name="baseText">Text, $ tag marks cursor </param>
        /// <param name="userInput">Text to input at cursor ($)</param>
        /// <param name="expectedOutput">Final text with replacements</param>
        public void AssertReplacement(string baseText, string userInput, string expectedOutput)
        {
            (int cursor, string inputText, string span) = TestUtils.PrepareTextWithSpan(baseText, userInput);

            TextManipulator manipulator = new TextManipulator(inputText, cursor);

            var change = new TextChange(cursor, userInput, cursor, span);
            var manipulations = manipulator.ManipulateText(change);
            var actualOutput = ApplyManipulations(inputText, manipulations);

            Assert.Equal(expectedOutput, actualOutput);
        }

        private string ApplyManipulations(string text, IList<TextManipulation> manipulations)
        {
            foreach(var manipulation in manipulations)
            {
                if(manipulation.Type == ManipulationType.Insert)
                {
                    text = text.Insert(manipulation.Start, manipulation.Text);
                }
                if(manipulation.Type == ManipulationType.Delete)
                {
                    text = text.Remove(manipulation.Start, manipulation.End - manipulation.Start);
                }
            }
            return text;
        }
    }

    public class TextChange : ITextChange
    {
        public static TextChange Insertion(int position, string text)
        {
            return new TextChange(position, text, position, "");
        }
        public static TextChange Replacement(int position, string newText, string oldText)
        {
            return new TextChange(position, newText, position, oldText);
        }

        public TextChange(int newPosition, string newText, int oldPosition, string oldText)
        {
            (NewPosition, NewText, OldPosition, OldText) = (newPosition, newText, oldPosition, oldText);
        }

        public int NewPosition { get; set; }

        public string NewText { get; set; }

        public int OldPosition { get; set; }

        public string OldText { get; set; }
    }
}
