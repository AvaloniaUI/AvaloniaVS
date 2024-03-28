using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Ide.CompletionEngine;
using Xunit;

namespace CompletionEngineTests.Parsing;

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
    public void Should_MoveBackTo0Nesting_When_ParsedDeclaretionTag()
    {
        var p = XmlParser.Parse("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
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

    [Theory]
    [InlineData("One_Level", 553, 1, 1, "Window")]
    [InlineData("One_Level_With_CDATA", 589, 1, 1, "Window")]
    [InlineData("One_Level_With_Comment", 576, 1, 1, "Window")]
    [InlineData("Two_Level", 578, 1, 2, "Window.Styles")]
    [InlineData("Two_Level_With_CDATA", 626, 1, 2, "Window.Styles")]
    [InlineData("Two_Level_With_Comment", 108, 1, 2, "Window.Styles")]
    public void Should_GetParentTagName_At_Level(string source, int position, int level, int nestingLevelExpected, string expectedParentTag)
    {
        var data = GetData(source);

        var state = XmlParser.Parse(data.AsMemory(), position, 0);
        Assert.NotNull(state);
        Assert.Equal(nestingLevelExpected, state.NestingLevel);
        var parentTag = state.GetParentTagName(level);
        Assert.Equal(expectedParentTag, parentTag);
    }

    [Theory]
    [InlineData("<UserControl x:DataType=\"Button\"><TextBlock Tag=\"\"")]
    [InlineData("<UserControl x:DataType= \"Button\"><TextBlock Tag=\"\"")]
    [InlineData("<UserControl x:DataType = \"Button\"><TextBlock Tag=\"\"")]
    [InlineData("<UserControl x:DataType =\"Button\"><TextBlock Tag=\"\"")]
    [InlineData("<UserControl x:DataType\t=\r\"Button\"><TextBlock Tag=\"\"")]
    [InlineData("<UserControl x:DataType\t=\n\"Button\"><TextBlock Tag=\"\"")]
    [InlineData("<UserControl x:DataType \t=\r\"Button\"><TextBlock Tag=\"\"")]
    [InlineData("<UserControl x:DataType\t =\r\"Button\"><TextBlock Tag=\"\"")]
    public void Should_FindParentAttributeValue(string source)
    {
        var state = XmlParser.Parse(source.AsMemory(),source.Length,0);
        Assert.NotNull(state.FindParentAttributeValue("(x\\:)?DataType"));
    }

    string GetData(string name, [CallerMemberName] string callerMethod = "")
    {
        var ass = this.GetType().Assembly;
        if (ass.GetManifestResourceNames()
             .FirstOrDefault(n => n.EndsWith($"{callerMethod}_{name}.xml")) is string resName)
        {
            using var stream = ass.GetManifestResourceStream(resName);
            return (new StreamReader(stream)).ReadToEnd();
        }
        return default(string);
    }
}
