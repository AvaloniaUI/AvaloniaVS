using System.Linq;
using Xunit;

namespace CompletionEngineTests
{
    public class BasicTests : XamlCompletionTestBase
    {
        [Fact]
        public void ClosingTagShouldBeProperlyCompleted()
        {
            AssertSingleCompletion("<UserControl><Button><Button.Styles><Style/></Button.Styles><", "/", "/Button>");
        }

        [Fact]
        public void Property_Should_Be_Renamed()
        {
            AssertSingleCompletionInMiddleOfText("<UserControl ", "=\"Top\"","HorizontalAlign", "HorizontalAlignment");
        }

        [Fact]
        public void Property_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl ", "HorizontalAlign", "HorizontalAlignment=\"\"");
        }

        [Fact]
        public void Property_Completions_Should_Be_Unique()
        {
            var compl = GetCompletionsFor("<UserControl P");
            Assert.All(compl.Completions.GroupBy(v => v.DisplayText), v => Assert.Single(v));
        }

        [Fact]
        public void Get_Only_Property_Should_Not_Be_Completed()
        {
            var compl = GetCompletionsFor("<UserControl P");

            Assert.All(compl.Completions, c => Assert.NotEqual("Parent", c.DisplayText));
        }

        [Fact]
        public void XmlContent_Property_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl><UserControl.", "HorizontalAlign", "HorizontalAlignment");
        }

        [Fact]
        public void AttachedProperty_Class_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl ", "Gri", "Grid.");
        }

        [Fact]
        public void XmlContent_AttachedProperty_Class_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl><", "Gri", "Grid");
        }

        [Fact]
        public void AttachedProperty_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Grid.", "Ro", "Row=\"\"");
        }

        [Fact]
        public void AttachedProperty_Should_Be_Renamed()
        {
            AssertSingleCompletionInMiddleOfText("<UserControl Grid.", "=\"2\"", "Ro", "Row");
        }

        [Fact]
        public void XmlContent_AttachedProperty_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl><Grid.", "Ro", "Row");
        }

        [Fact]
        public void EnumValue_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl HorizontalAlignment=\"", "Le", "Left");
        }

        [Fact]
        public void WellKnown_UrlNameSpaces_Should_Be_Completed()
        {
            var compl = GetCompletionsFor("<UserControl xmlns:t=\"http");

            Assert.NotEmpty(compl.Completions);
            Assert.Contains(compl.Completions, v => v.InsertText == "https://github.com/avaloniaui");
            Assert.Contains(compl.Completions, v => v.InsertText == "http://schemas.microsoft.com/winfx/2006/xaml");
        }

        [Fact]
        public void Clr_NameSpaces_Should_Be_Completed()
        {
            var compl = GetCompletionsFor("<UserControl xmlns:t=\"clr-namespace:Ava");

            Assert.NotEmpty(compl.Completions);
            Assert.Contains(compl.Completions, v => v.InsertText == "clr-namespace:Avalonia.Data;assembly=Avalonia.Base");
            Assert.Contains(compl.Completions, v => v.InsertText == "clr-namespace:Avalonia.Controls;assembly=Avalonia.Controls");
        }

        [Fact]
        public void Using_NameSpaces_Should_Be_Completed()
        {
            var compl = GetCompletionsFor("<UserControl xmlns:t=\"using:Ava");

            Assert.NotEmpty(compl.Completions);
            Assert.Contains(compl.Completions, v => v.InsertText == "using:Avalonia.Data");
            Assert.Contains(compl.Completions, v => v.InsertText == "using:Avalonia.Controls");
        }

        [Fact]
        public void Extension_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Content=\"{", "Bind", "Binding");
        }

        [Fact]
        public void Extension_Property_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Content=\"{Binding ", "Pa", "Path=");
        }

        [Fact]
        public void Extension_Property_Enum_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Content=\"{Binding Mode=", "One", "OneWay");
        }
        
        [Fact]
        public void Extension_DataType_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl ", "x:Data", "x:DataType=\"\"");
        }
    }
}
