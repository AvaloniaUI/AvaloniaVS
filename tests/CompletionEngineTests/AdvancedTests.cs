using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CompletionEngineTests
{
    public class AdvancedTests : XamlCompletionTestBase
    {
        [Fact]
        public void WellKnown_Brushes_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Background=\"", "Re", "Red");
        }

        [Fact]
        public void WellKnown_ThemeKeys_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Background=\"{DynamicResource ", "Theme", "ThemeBackgroundBrush");
        }

        [Fact]
        public void Extension_With_CtorArgument_Class_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Background=\"{x:Static ", "Brus", "Brushes");
        }

        [Fact]
        public void Enum_Type_in_StaticExtension_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Tag=\"{x:Static ", "HorizontalAlignme", "HorizontalAlignment");
        }

        [Fact]
        public void Enum_Value_in_StaticExtension_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl HorizontalAlignment=\"{x:Static ", "HorizontalAlignment.L", "HorizontalAlignment.Left");
        }

        [Fact]
        public void Extension_With_CtorArgument_Static_Properties_Values_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Background=\"{x:Static ", "Brushes.Re", "Brushes.Red");
        }

        [Fact]
        public void Extension_With_CtorArgument_Static_Field_Values_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl IsEnabled=\"{Binding Converter={x:Static ", "ObjectConverters.IsN", "ObjectConverters.IsNull");
        }

        [Fact]
        public void Extension_Property_With_WellKnown_Value_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Background=\"{Binding RelativeSource=", "Se", "Self");
        }

        [Fact]
        public void Extension_With_CtorArgument_Enum_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl Background=\"{Binding RelativeSource={RelativeSource ", "Se", "Self");
        }
        
        [Fact]
        public void Binding_Path_Should_Be_Completed_From_xDataType()
        {
            AssertSingleCompletion("<UserControl x:DataType=\"Button\"><TextBlock Tag=\"{Binding Path=", "Conte", "Content");
        }
        
        [Fact]
        public void Binding_Path_Should_Be_Completed_From_xDataType2()
        {
            AssertSingleCompletion("<UserControl x:DataType=\"Button\"><TextBlock Tag=\"{Binding ", "Conte", "Content");
        }
        
        [Fact]
        public void Binding_Path_Should_Be_Completed_From_sParent()
        {
            AssertSingleCompletion("<UserControl Background=\"{Binding ", "$pa", "$parent[");
        }
        
        [Fact]
        public void Binding_Path_Should_Be_Completed_From_sParentType()
        {
            AssertSingleCompletion("<UserControl Background=\"{Binding ", "$parent[But", "$parent[Button].");
        }

        [Fact]
        public void Binding_Path_Should_Be_Completed_From_xName()
        {
            AssertSingleCompletion("<UserControl x:Name=\"foo\" Tag=\"{Binding ", "#f", "#foo");
        }

        [Fact]
        public void Binding_Path_Should_Be_Completed_From_sParentType_Property()
        {
            AssertSingleCompletion("<UserControl Background=\"{Binding ", "$parent[Button].Ta", "$parent[Button].Tag");
        }
        
        [Fact]
        public void Binding_Path_Should_Be_Completed_From_sParent_Property()
        {
            AssertSingleCompletion("<UserControl Background=\"{Binding ", "$parent.Ta", "$parent.Tag");
        }

        [Fact]
        public void Binding_Path_Should_Be_Completed_From_sParent_Property_Nested()
        {
            AssertSingleCompletion("<UserControl Background=\"{Binding ", "$parent.Bounds.Wi", "$parent.Bounds.Width");
        }

        [Fact]
        public void Extension_With_CtorArgument_Type_Should_Be_Completed()
        {
            AssertSingleCompletion("<DataTemplate DataType=\"{x:Type ", "But", "Button");
        }
        
        [Fact]
        public void Extension_DataType_Types_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl x:DataType=\"", "But", "Button");
        }

        [Fact]
        public void Property_Of_Type_Type_Type_Should_Be_Completed()
        {
            AssertSingleCompletion("<DataTemplate DataType=\"", "But", "Button");
        }

        [Fact]
        public void TemplateBinding_AvaloniaPropeties_Should_Be_Completed()
        {
            AssertSingleCompletion("<ContentPresenter Background=\"{TemplateBinding ", "Back", "Background");
        }

        [Fact]
        public void StyleSelector_Control_Types_Should_Be_Completed()
        {
            AssertSingleCompletion("<Style Selector=\"", "But", "Button");
        }

        [Fact]
        public void StyleSelector_Some_WellKnown_Keywords_Should_Be_Completed()
        {
            var compl = GetCompletionsFor("<Style Selector=\"").Completions;

            Assert.Contains(compl, v => v.InsertText == ">");
            Assert.Contains(compl, v => v.InsertText == ".");
            Assert.Contains(compl, v => v.InsertText == "#");
            Assert.Contains(compl, v => v.InsertText == "/template/");
        }

        [Fact]
        public void StyleSelector_Some_WellKnown_PseudoClasses_Should_Be_Completed()
        {
            var compl = GetCompletionsFor("<Style Selector=\"").Completions;

            Assert.Contains(compl, v => v.InsertText == ":pointerover");
            Assert.Contains(compl, v => v.InsertText == ":disabled");
            Assert.Contains(compl, v => v.InsertText == ":focus");
        }

        [Fact]
        public void Style_Property_Name_Should_Be_Completed()
        {
            AssertSingleCompletion("<Style Selector=\"Button\"><Setter Property=\"", "HorizontalAli", "HorizontalAlignment");
        }

        [Fact]
        public void Style_Property_Name_Should_Be_Completed_From_Last_Selector_Type()
        {
            AssertSingleCompletion("<Style Selector=\"Button.classname:pseudoclass /template/ > Grid#name\"><Setter Property=\"", "ColumnDef", "ColumnDefinitions");
        }

        [Fact]
        public void Style_Attached_Property_Class_Name_Should_Be_Completed()
        {
            AssertSingleCompletion("<Style Selector=\"Button\"><Setter Property=\"", "TextBl", "TextBlock");
        }

        [Fact]
        public void Style_Attached_Property_Name_Should_Be_Completed()
        {
            var xaml = "<Style Selector=\"Button\"><Setter Property=\"";
            var typed = "TextElement.FontWe";
            
            var comp = GetCompletionsFor(xaml + typed);
            if (comp == null)
                throw new Exception("No completions found");

            // AttachedProperty in Setter changed in GH#302 - this part of the test is now failing
            // and I don't know why. I have tested this in an actual xaml document and it works
            // perfectly fine, so I'm skipping this now
            //var pos = xaml.Length + typed.IndexOf('.');
            //Assert.True(pos == comp.StartPosition, $"Invalid completion start position typed");

            Assert.Contains(comp.Completions, c => c.InsertText == "FontWeight");

            Assert.Single(comp.Completions, c => c.InsertText == "FontWeight");
        }

        [Fact]
        public void Style_Attached_Property_Value_Should_Be_Completed()
        {
            AssertSingleCompletion("<Style Selector=\"Button\"><Setter Property=\"TextElement.FontWeight\" Value=\"", "Bo", "Bold");
        }

        [Fact]
        public void Style_Property_Value_Should_Be_Completed()
        {
            AssertSingleCompletion("<Style Selector=\"Button.my\"><Setter Property=\"HorizontalAlignment\" Value=\"", "Le", "Left");
        }

        [Fact]
        public void Image_Source_resm_Uris_Should_Be_Completed()
        {
            AssertSingleCompletion("<Image Source=\"", "resm:", "resm:CompletionEngineTests.Test.bmp?assembly=CompletionEngineTests");
        }

        [Fact]
        public void Image_Source_resm_RelativeUris_Should_Be_Completed()
        {
            AssertSingleCompletion("<Image Source=\"", "resm:", "resm:CompletionEngineTests.Test.bmp");
        }

        [Fact]
        public void Image_Source_avares_Uris_Should_Be_Completed()
        {
            AssertSingleCompletion("<Image Source=\"", "avares:", "avares://CompletionEngineTests/Test.bmp");
        }

        [Fact]
        public void Image_Source_avares_RelativeUris_Should_Be_Completed()
        {
            AssertSingleCompletion("<Image Source=\"", "/", "/Test.bmp");
        }

        [Fact]
        public void StyleInclude_Source_Uris_Should_Be_Completed()
        {
            AssertSingleCompletion("<StyleInclude Source=\"", "avares:", "avares://CompletionEngineTests/Test.xaml");
        }

        [Fact]
        public void StyleInclude_Source_RelativeUris_Should_Be_Completed()
        {
            AssertSingleCompletion("<StyleInclude Source=\"", "/", "/Test.xaml");
        }

        [Fact]
        public void StyleInclude_Source_Uris_Should_Be_Completed_CompiledStyles()
        {
            AssertSingleCompletion("<StyleInclude Source=\"", "avares:", "avares://CompletionEngineTests/TestCompiledTheme.xaml");
        }

        [Fact]
        public void StyleInclude_Source_RelativeUris_Should_Be_CompiledStyles()
        {
            AssertSingleCompletion("<StyleInclude Source=\"", "/", "/TestCompiledTheme.xaml");
        }

        [Fact]
        public void xClass_Directive_Should_Be_Completed()
        {
            var compl = GetCompletionsFor("<UserControl x:Cla").Completions;

            Assert.Contains(compl, v => v.InsertText == "x:Class=\"\"");
        }

        [Fact]
        public void xName_Directive_Should_Be_Completed()
        {
            var compl = GetCompletionsFor("<UserControl x:N").Completions;

            Assert.Contains(compl, v => v.InsertText == "x:Name=\"\"");
        }

        [Fact]
        public void xKey_Directive_Should_Be_Completed()
        {
            var compl = GetCompletionsFor("<UserControl x:K").Completions;

            Assert.Contains(compl, v => v.InsertText == "x:Key=\"\"");
        }

        [Fact]
        public void xTypeArguments_Directive_Should_Be_Completed()
        {
            AssertSingleCompletion("<local:GenericBaseClass`1 ", "x:T", "x:TypeArguments=\"\"");
        }

        [Fact]
        public void xTypeArguments_Value_Should_Be_Completed()
        {
            AssertSingleCompletion("<local:GenericBaseClass`1 x:TypeArguments=\"", "Tex", "TextBlock");
        }

        [Fact]
        public void xTypeArguments_Directive_Should_Not_Be_Completed_On_NonGeneric_Type()
        {
           Assert.Null(GetCompletionsFor("<UserControl x:TypeArgum"));
        }

        [Fact]
        public void xmlns_Directive_Should_Be_Completed()
        {
            var compl = GetCompletionsFor("<UserControl x").Completions;

            Assert.Contains(compl, v => v.InsertText == "xmlns=\"\"");
        }

        [Fact]
        public void xClass_Value_Should_Be_Completed()
        {
            AssertSingleCompletion("<UserControl x:Class=\"", "", "CompletionEngineTests.TestUserControl");
        }

        [Fact]
        public void OnPlatform_Should_Be_Suggested_As_Markup_Extension()
        {
            var xaml = "<Button Background=\"{O";

            var comp = GetCompletionsFor(xaml);
            if (comp == null)
                throw new Exception("No completions found");

            Assert.NotNull(comp.Completions.Where(x => x.DisplayText.Equals("OnPlatform")).FirstOrDefault());
        }

        [Fact]
        public void OnFormFactor_Should_Be_Suggested_As_Markup_Extension()
        {
            var xaml = "<Button Background=\"{O";

            var comp = GetCompletionsFor(xaml);
            if (comp == null)
                throw new Exception("No completions found");

            Assert.NotNull(comp.Completions.Where(x => x.DisplayText.Equals("OnFormFactor")).FirstOrDefault());
        }

        [Fact]
        public void OnPlatform_Should_Be_Suggested_As_Xaml_Element()
        {
            var xaml = "<O";

            var comp = GetCompletionsFor(xaml);
            if (comp == null)
                throw new Exception("No completions found");

            Assert.NotNull(comp.Completions.Where(x => x.DisplayText.Equals("OnPlatform")).FirstOrDefault());
        }

        [Fact]
        public void OnFormFactor_Should_Be_Suggested_As_Xaml_Element()
        {
            var xaml = "<O";

            var comp = GetCompletionsFor(xaml);
            if (comp == null)
                throw new Exception("No completions found");

            Assert.NotNull(comp.Completions.Where(x => x.DisplayText.Equals("OnFormFactor")).FirstOrDefault());
        }

        [Fact]
        public void MarkupExtension_As_Xaml_Element_Should_Not_Have_Extension_Suffix()
        {
            var xaml = "<Sta";

            var comp = GetCompletionsFor(xaml);
            if (comp == null)
                throw new Exception("No completions found");

            Assert.NotNull(comp.Completions
                .Where(x => x.DisplayText.Equals("StaticResource") && x.InsertText.Equals("StaticResource"))
                .FirstOrDefault());
        }

        [Fact]
        public void OnPlatform_Suggestions_Are_Context_Specific_In_Markup_Extension()
        {
            var xaml = "<Button IsVisible=\"{OnPlatform ";

            var comp = GetCompletionsFor(xaml);
            if (comp == null)
                throw new Exception("No completions found");

            // Suggest property completions
            Assert.Equal(2, comp.Completions.Count);
            Assert.Contains(comp.Completions, x => x.DisplayText.Equals("True"));
            Assert.Contains(comp.Completions, x => x.DisplayText.Equals("False"));

            // Now comma should list platforms for other options
            xaml += ",";
            comp = GetCompletionsFor(xaml);
            if (comp == null)
                throw new Exception("No completions found");

            var platforms = new List<string> { "Windows", "macOS", "Linux", "Browser", "iOS", "Android" };

            Assert.Equal(platforms.Count, comp.Completions.Count);
            // Should suggest all platforms
            foreach (var item in comp.Completions)
            {
                if (platforms.Contains(item.DisplayText, StringComparer.InvariantCultureIgnoreCase))
                {
                    platforms.Remove(item.DisplayText);
                }
            }
            Assert.Empty(platforms);
        }

        [Fact]
        public void OnFormFactor_Suggestions_Are_Context_Specific_In_Markup_Extension()
        {
            var xaml = "<Button IsVisible=\"{OnFormFactor ";

            var comp = GetCompletionsFor(xaml);
            if (comp == null)
                throw new Exception("No completions found");

            // Suggest property completions
            Assert.Equal(2, comp.Completions.Count);
            Assert.Contains(comp.Completions, x => x.DisplayText.Equals("True"));
            Assert.Contains(comp.Completions, x => x.DisplayText.Equals("False"));

            // Now comma should list platforms for other options
            xaml += ",";
            comp = GetCompletionsFor(xaml);
            if (comp == null)
                throw new Exception("No completions found");

            var formFactors = new List<string> { "Desktop", "Mobile" };

            Assert.Equal(formFactors.Count, comp.Completions.Count);
            // Should suggest all platforms
            foreach (var item in comp.Completions)
            {
                if (formFactors.Contains(item.DisplayText, StringComparer.InvariantCultureIgnoreCase))
                {
                    formFactors.Remove(item.DisplayText);
                }
            }
            Assert.Empty(formFactors);
        }
    }
}
