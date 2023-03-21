using Avalonia.Ide.CompletionEngine;
using Xunit;

namespace CompletionEngineTests.Parsing
{
    public class SelectorParserTest
    {
        [Fact]
        public void Parse_Is_Selector()
        {
            var parser = SelectorParser.Parse(":is(B");

            Assert.Equal(SelectorStatement.FunctionArgs, parser.PreviousStatement);
            Assert.Equal("is", parser.FunctionName);
            Assert.Equal(SelectorStatement.TypeName, parser.Statement);
            Assert.Equal("B", parser.TypeName);
        }

        [Fact]
        public void Parse_Not_Selector()
        {
            var parser = SelectorParser.Parse(":not(B");

            Assert.Equal(SelectorStatement.CanHaveType, parser.PreviousStatement);
            Assert.Equal("not", parser.FunctionName);
            Assert.Equal(SelectorStatement.FunctionArgs, parser.Statement);
            Assert.Equal("B", parser.TypeName);
        }

        [Fact]
        public void Parse_not_infinite_loop()
        {
            var parser = SelectorParser.Parse("Button:not(:disabled)");

            Assert.Equal(SelectorStatement.FunctionArgs, parser.PreviousStatement);
            Assert.Equal("not", parser.FunctionName);
            Assert.Equal(SelectorStatement.Middle, parser.Statement);
            Assert.Equal("disabled", parser.Class);
        }

        [Fact]
        public void Parse_Collon_After_Property_Selector()
        {
            var parser = SelectorParser.Parse("Button[IsDefault=True]:");

            Assert.Equal(SelectorStatement.Middle, parser.PreviousStatement);
            Assert.Equal("IsDefault", parser.PropertyName);
            Assert.Equal(SelectorStatement.Colon, parser.Statement);
            Assert.Equal("", parser.Class);
        }

    }
}
