using Avalonia.Ide.CompletionEngine;
using Xunit;

namespace CompletionEngineTests
{
    public class SelectorParserTest
    {
        [Fact]
        public void Parse_Is_Selector()
        {
            var parser = SelectorParser.Parse(":is(B");

            Assert.Equal(SelectorStatment.FunctionArgs, parser.PreviusStatment);
            Assert.Equal("is", parser.FunctionName);
            Assert.Equal(SelectorStatment.TypeName, parser.Statment);
            Assert.Equal("B", parser.TypeName);
        }

        [Fact]
        public void Parse_Not_Selector()
        {
            var parser = SelectorParser.Parse(":not(B");

            Assert.Equal(SelectorStatment.CanHaveType, parser.PreviusStatment);
            Assert.Equal("not", parser.FunctionName);
            Assert.Equal(SelectorStatment.FunctionArgs, parser.Statment);
            Assert.Equal("B", parser.TypeName);
        }

        [Fact]
        public void Parse_not_infinite_loop()
        {
            var parser = SelectorParser.Parse("Button:not(:disabled)");

            Assert.Equal(SelectorStatment.FunctionArgs, parser.PreviusStatment);
            Assert.Equal("not", parser.FunctionName);
            Assert.Equal(SelectorStatment.Middle, parser.Statment);
            Assert.Equal("disabled", parser.Class);
        }

        [Fact]
        public void Parse_Collon_After_Property_Selector()
        {
            var parser = SelectorParser.Parse("Button[IsDefault=True]:");

            Assert.Equal(SelectorStatment.Middle, parser.PreviusStatment);
            Assert.Equal("IsDefault", parser.PropertyName);
            Assert.Equal(SelectorStatment.Colon, parser.Statment);
            Assert.Equal("", parser.Class);
        }

    }
}
