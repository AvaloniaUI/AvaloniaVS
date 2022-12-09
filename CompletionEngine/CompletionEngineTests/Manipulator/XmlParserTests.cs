using System;
using Avalonia.Ide.CompletionEngine;
using Xunit;

namespace CompletionEngineTests.Manipulator
{
    /// <summary>
    /// Tests for XmlParser behavior on which TextManipulator is dependent
    /// </summary>
    public class XmlParserTests
    {
        [Fact]
        public void Should_BeInNoneState_When_OnClosingBrace()
        {
            var parser = XmlParser.Parse("<Grid>");
            Assert.Equal(XmlParser.ParserState.None, parser.State);
        }

        [Fact]
        public void Should_NotBeInClosingTag_When_StartTag()
        {
            var p = XmlParser.Parse("<Grid");
            Assert.False(p.IsInClosingTag);
        }

        [Fact]
        public void Should_BeInClosingTag_When_ParsedSlash()
        {
            var p = XmlParser.Parse("<Grid></");
            Assert.True(p.IsInClosingTag);
        }

        [Fact]
        public void Should_BeInClosingTag_When_InsideEndTag()
        {
            var p = XmlParser.Parse("<Grid></Grid");
            Assert.True(p.IsInClosingTag);
        }

        [Fact]
        public void Should_MoveBackTo0Nesting_When_ParsedClosedTag()
        {
            var p = XmlParser.Parse("<Grid><Foo></Foo></");
            Assert.Equal(0, p.NestingLevel);
        }

        [Fact]
        public void Should_MoveBackTo0Nesting_When_ParsedSelfclosedTag()
        {
            var p = XmlParser.Parse("<Grid><Foo/></");
            Assert.Equal(0, p.NestingLevel);
        }

        [Fact]
        public void Should_ReturnCorrectTagName()
        {
            var p = XmlParser.Parse("<Grid><Tag Attribute=\"\"/");
            Assert.Equal("Tag", p.ParseCurrentTagName());
        }

        [Fact]
        public void Should_SeekEndTag_In_SimpleCase()
        {
            string data = "<Grid></Grid>";
            int ppos = "<Grid".Length;
            int seek = "<Grid></".Length;

            var p = XmlParser.Parse(data.AsMemory(), 0, ppos);
            var result = p.SeekClosingTag();

            Assert.True(result);
            Assert.Equal(seek, p.ParserPos);
        }

        [Fact]
        public void Should_SeekEndTag_In_OverClosedTag()
        {
            string data = "<Grid><Foo/></Grid>";
            int ppos = "<Grid".Length;
            int seek = "<Grid><Foo/></".Length;

            var p = XmlParser.Parse(data.AsMemory(), 0, ppos);
            var result = p.SeekClosingTag();

            Assert.True(result);
            Assert.Equal(seek, p.ParserPos);
        }

        [Fact]
        public void Should_Fail_On_InavlidNesting()
        {
            string data = "<Grid><Foo></Grid>";
            int ppos = "<Grid".Length;
            int seek = data.Length;

            var p = XmlParser.Parse(data.AsMemory(), 0, ppos);
            var result = p.SeekClosingTag();

            Assert.False(result);
            Assert.Equal(seek, p.ParserPos);
        }
    }
}
