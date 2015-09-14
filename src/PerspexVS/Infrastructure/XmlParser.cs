using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox
{
    public class XmlParser
    {
        private readonly string _data;

        public enum ParserState
        {
            None,
            InsideComment,
            InsideCdata,
            StartElement,
            InsideLement,
            StartAttribute,
            BeforeAttributeValue,
            AttributeValue,
        }

        public ParserState State { get; set; }

        private int _elementNameStart;
        private int _attributeNameStart;
        private int? _elementNameEnd;
        private int? _attributeNameEnd;
        private int _attributeValueStart;

        public string TagName => State >= ParserState.StartElement
            ? _data.Substring(_elementNameStart, (_elementNameEnd ?? (_data.Length - 1)) - _elementNameStart + 1)
            : null;

        public string AttributeName => State >= ParserState.StartAttribute
            ? _data.Substring(_attributeNameStart, (_attributeNameEnd ?? (_data.Length - 1)) - _attributeNameStart + 1)
            : null;

        public string AttributeValue =>
            State == ParserState.AttributeValue ? _data.Substring(_attributeValueStart) : null;

        public int? CurrentValueStart =>
            State == ParserState.StartElement
                ? _elementNameStart
                : State == ParserState.StartAttribute
                    ? _attributeNameStart
                    : State == ParserState.AttributeValue
                        ? _attributeValueStart
                        : (int?) null;

        XmlParser(string data)
        {
            _data = data;
        }

        private const string CommentStart = "!--";
        private const string CommentEnd = "-->";

        private const string CdataStart = "![CDATA[";
        private const string CdataEnd = "]]>";
        
        bool CheckPrev(int caret, string checkFor)
        {
            var startAt = caret - checkFor.Length + 1;
            if (startAt < 0)
                return false;
            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var c = 0; c < checkFor.Length; c++)
            {
                if (_data[c + startAt] != checkFor[c])
                    return false;
            }
            return true;
        }

        void Parse()
        {
            for (var i = 0; i < _data.Length; i++)
            {
                var c = _data[i];
                if (c == '<' && State == ParserState.None)
                {
                    State = ParserState.StartElement;
                    _elementNameStart = i + 1;
                    _elementNameEnd = null;
                }
                else if (State == ParserState.StartElement && CheckPrev(i, CommentStart))
                {
                    State = ParserState.InsideComment;
                }
                else if (State == ParserState.InsideComment && CheckPrev(i, CommentEnd))
                {
                    State = ParserState.None;
                }
                else if (State == ParserState.StartElement && CheckPrev(i, CdataStart))
                {
                    State = ParserState.InsideCdata;
                }
                else if (State == ParserState.InsideCdata && CheckPrev(i, CdataEnd))
                {
                    State = ParserState.None;
                }
                else if (State == ParserState.StartElement && char.IsWhiteSpace(c))
                {
                    State = ParserState.InsideLement;
                    _elementNameEnd = i - 1;
                }
                else if ((State == ParserState.InsideLement || State == ParserState.StartElement) && c == '>')
                {
                    State = ParserState.None;
                }
                else if (State == ParserState.InsideLement && !char.IsWhiteSpace(c))
                {
                    State = ParserState.StartAttribute;
                    _attributeNameStart = i;
                    _attributeNameEnd = null;
                }
                else if (State == ParserState.StartAttribute && (c == '=' || char.IsWhiteSpace(c)))
                {
                    State = ParserState.BeforeAttributeValue;
                    _attributeNameEnd = i - 1;
                }
                else if (State == ParserState.BeforeAttributeValue && c == '"')
                {
                    State = ParserState.AttributeValue;
                    _attributeValueStart = i + 1;
                }
                else if (State == ParserState.AttributeValue && c == '"')
                {
                    State = ParserState.InsideLement;
                }
            }
        }

        public static XmlParser Parse(string data)
        {
            var rv = new XmlParser(data);
            rv.Parse();
            return rv;
        }

        public override string ToString()
        {
            return $"State: {State}, TagName: {TagName}, AttributeName: {AttributeName}, Attribute: {AttributeValue}";
        }
    }
}
