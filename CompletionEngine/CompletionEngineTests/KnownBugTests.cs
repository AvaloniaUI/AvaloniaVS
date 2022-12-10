using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Xunit;

namespace CompletionEngineTests
{
    public class KnownBugTests : XamlCompletionTestBase
    {
        [Fact]
        public void CompletionShouldRecognizeDoubleTransition()
        {
            AssertSingleCompletion("<", "DoubleTra", "DoubleTransition");
        }

        [Fact]
        public void CompletionShouldShowPropertiesFromBaseClasses()
        {
            AssertSingleCompletion("<local:EmptyClassDerivedFromGenericClassWithDouble ", "Generic", "GenericProperty=\"\"");
        }

        [Theory,
            InlineData("Animations")
        ]
        public void StylePropertiesShouldBeShown(string propertyName)
        {
            AssertSingleCompletion("<UserControl><UserControl.Styles><Style><Style.", propertyName.Substring(0, 1),
                propertyName);
        }

        [Theory,
            InlineData("Item")]
        public void NonStylePropertiesShouldNotBeShownOnStyle(string propertyName)
        {
            var comp = GetCompletionsFor("<UserControl><UserControl.Styles><Style><Style." +
                                         propertyName.Substring(0, 1));
            if (comp == null)
                return;
            Assert.Empty(comp.Completions.Where(c => c.InsertText.StartsWith(propertyName)));
        }

        [Fact]
        public void InterfacePropertiesShouldNotBeShown()
        {
            Assert.Empty(GetCompletionsFor("<Button ").Completions.Where(c => c.InsertText.Contains("IStyleable")));
        }
        
        [Fact]
        public void OnlyAttachedPropertiesShouldBeShownInDottedXamlTag()
        {
            var gridAttachedProperties = new HashSet<string>(typeof(Grid)
                .GetFields(BindingFlags.Public | BindingFlags.Static).Where(p =>
                    p.FieldType.IsConstructedGenericType
                    && p.FieldType.GetGenericTypeDefinition() == typeof(AttachedProperty<>))
                .Select(p => p.Name.Replace("Property", "")));
            var completions = GetCompletionsFor("<UserControl><Grid.").Completions;
            foreach (var c in completions)
            {
                Assert.True(gridAttachedProperties.Contains(c.DisplayText), "Non-attached property " + c.DisplayText);
            }

            foreach (var a in gridAttachedProperties)
            {
                Assert.True(completions.Any(c => c.DisplayText == a), "Attached property " + a + " is not shown");
            }

        }
    }
}