using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Ide.CompletionEngine;

namespace AvaloniaVS.Shared.Completion.Manipulation
{
    public class AvVSTextManipulator
    {
        private readonly ReadOnlyMemory<char> _text;
        private readonly int _position;
        private readonly XmlParser _state;

        public AvVSTextManipulator(string text, int position)
        {
            _position = position;
            _text = text.AsMemory();

            int parserStart = 0;
            int parserEnd = 0;

            // To improve performance parse only last tag
            if (text.Length > 0)
            {
                // Findl last < tag
                parserStart = position;
                if (position >= text.Length)
                {
                    parserStart = text.Length - 1;
                }
                parserStart = text.LastIndexOf('<', parserStart);
                if (parserStart < 0)
                {
                    parserStart = 0;
                }


                if (text.Length > position)
                {
                    parserEnd = position;
                }
                else
                {
                    parserEnd = text.Length;
                }
            }


            _state = XmlParser.Parse(_text, parserStart, parserEnd);
        }

        public IList<TextManipulation> ManipulateText(ITextChange textChange)
        {
            List<TextManipulation> maniplations = new List<TextManipulation>();
            if (_state.State == XmlParser.ParserState.StartElement
                || (_state.State == XmlParser.ParserState.None && _text.Span[_state.ParserPos] == '>')
                )
            {
                SynchronizeStartAndEndTag(textChange, maniplations);
            }


            if (_state.State == XmlParser.ParserState.StartElement
            || _state.State == XmlParser.ParserState.AfterAttributeValue
            || _state.State == XmlParser.ParserState.InsideElement)
            {

                new AvVSCloseXmlTagManipulation(_state, _text, _position).TryCloseTag(textChange, maniplations);
            }

            return maniplations.OrderByDescending(n => n.Start).ToList();
        }

        private char[] XmlNameSpecialCharacters = new[] { '-', '_', '.' };

        private void SynchronizeStartAndEndTag(ITextChange textChange, List<TextManipulation> maniplations)
        {
            if (!textChange.NewText.All(n => char.IsLetterOrDigit(n) || XmlNameSpecialCharacters.Contains(n)))
            {
                return;
            }

            string startTag = _state.ParseCurrentTagName();
            int? maybeTagStart = _state.CurrentValueStart;
            if (maybeTagStart == null)
            {
                return;
            }

            int startPos = maybeTagStart.Value; // add 1 to take opening < into account
            if (startTag.EndsWith("/"))
            {
                return; // start tag is self-closing
            }
            if (textChange.NewPosition < startPos || textChange.NewPosition > startPos + startTag.Length)
            {
                return; //we are not editing tag name
            }

            XmlParser searchEndTag = _state.Clone();
            if (searchEndTag.SeekClosingTag())
            {
                string endTag = searchEndTag.ParseCurrentTagName();
                if (endTag[0] != '/')
                {
                    return;
                }

                maybeTagStart = searchEndTag.CurrentValueStart;
                if (maybeTagStart == null)
                {
                    return;
                }

                int endPos = maybeTagStart.Value; // add 1 to take opening < into account

                // reverse change to start tag
                startTag = textChange.ReverseOn(startTag, startPos);

                bool isTheSameTag = endTag.Length > 0 && endTag.Substring(1) == startTag;
                if (isTheSameTag)
                {
                    maniplations.AddRange(textChange.AsManipulations(endPos - startPos));
                }
            }
        }
    }
}
