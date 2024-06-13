extern alias A1;
extern alias A2;

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Ide.CompletionEngine;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;
using Xunit;

namespace CompletionEngineTests
{
    public class MetadataConverterTests
    {
        [Fact]
        public void Should_Retrive_Type_When_Mixing_Assembly_Versions()
        {
            var clrType = typeof(Avalonia.Labs.Controls.Swipe);
            var nsName = "clr-namespace:" + clrType.Namespace + ";assembly=" + typeof(Avalonia.Labs.Controls.Swipe).Assembly.GetName().Name;
            var ns = Metadata.Namespaces[nsName];
            var type = ns[clrType.Name];
            var leftProperty = type.Properties.FirstOrDefault(p => p.Name == nameof(Avalonia.Labs.Controls.Swipe.Left));
            Assert.NotNull(leftProperty);
            Assert.NotNull(leftProperty.Type);
        }

        [Fact]
        public void DiscoverAttachedEvent_IfItIsDerivedFromRoutedEvent()
        {
            Type clrType = typeof(MetadataTestClass);
            string nsName = "clr-namespace:" + clrType.Namespace + ";assembly=" + typeof(MetadataTestClass).Assembly.GetName().Name;
            Dictionary<string, MetadataType> ns = Metadata.Namespaces[nsName];
            MetadataType type = ns[clrType.Name];

            var attachedEvent = type.Events.Single();
            Assert.True(attachedEvent.Type.FullName == typeof(MetadataTestClass).FullName);
        }


        [Fact]
        public void Discover_Do_Not_Overlapped()
        {
            Type clrType = typeof(Models.AttachedBehavior);
            string nsName = "clr-namespace:" + clrType.Namespace + ";assembly=" + clrType.Assembly.GetName().Name;
            Dictionary<string, MetadataType> ns = Metadata.Namespaces[nsName];

            Assert.NotNull(ns);
            MetadataType type = ns[clrType.Name];
            Assert.NotNull(type);
            Assert.Equal(GetName(clrType), type.AssemblyQualifiedName);

            Type clrTypeA1 = typeof(A1::CompletionEngineTests.Models.AttachedBehavior);
            nsName = "clr-namespace:" + clrTypeA1.Namespace + ";assembly=" + clrTypeA1.Assembly.GetName().Name;

            ns = Metadata.Namespaces[nsName];

            Assert.NotNull(ns);

            MetadataType typeA1 = ns[clrTypeA1.Name];

            Assert.NotNull(typeA1);

            Assert.Equal(GetName(clrTypeA1), typeA1.AssemblyQualifiedName);


            Type clrTypeA2 = typeof(A2::CompletionEngineTests.Models.AttachedBehavior);
            nsName = "clr-namespace:" + clrTypeA1.Namespace + ";assembly=" + clrTypeA2.Assembly.GetName().Name;

            ns = Metadata.Namespaces[nsName];

            Assert.NotNull(ns);

            MetadataType typeA2 = ns[clrTypeA2.Name];

            Assert.NotNull(typeA2);

            Assert.Equal(GetName(clrTypeA2), typeA2.AssemblyQualifiedName);
        }

        [Theory]
        [MemberData(nameof(GetCasese))]
        public void Discover_InternalsVisibleTo(TestScenario scenario)
        {
            Assert.NotNull(scenario.ClrType);
            string nsName = "clr-namespace:" + scenario.ClrType.Namespace + ";assembly=" + scenario.ClrType.Assembly.GetName().Name;
            Dictionary<string, MetadataType> ns = Metadata.Namespaces[nsName];

            Assert.NotNull(ns);
            ns.TryGetValue(scenario.ClrType.Name, out var type);
            scenario.CheckAction(scenario.ClrType, type);
        }

        [Fact]
        public void AttachedPropertySetterAndGetterMixmatch()
        {
            var clrType = typeof(Grid);
            string nsName = "clr-namespace:" + clrType.Namespace + ";assembly=" + clrType.Assembly.GetName().Name;
            var ns = Metadata.Namespaces[nsName];
            Assert.NotNull(ns);
            ns.TryGetValue(clrType.Name, out var type);
            Assert.NotNull(type);

            var property = type.Properties.SingleOrDefault(p => p.Name == "Column");
            Assert.NotNull(property);
            Assert.True(property.IsAttached);
            Assert.Equal("System.Int32", property.Type?.Name);
        }

        public static IEnumerable<object[]> GetCasese()
        {
            // Local Assembly
            yield return new object[]
            {
                 new TestScenario("Local Internal Attached Behavior",
                 typeof(Models.InternalAttachedBehavior),
                 new Action<Type,MetadataType>(static (clrType,mdType) =>
                 {
                     Assert.Equal(GetName(clrType) , mdType.AssemblyQualifiedName);
                 })),
            };
            yield return new object[]
            {
                 new TestScenario("Local Internal Class",
                 typeof(Models.InternalClass),
                 new Action<Type,MetadataType>(static (clrType,mdType) =>
                 {
                     Assert.Equal(GetName(clrType), mdType.AssemblyQualifiedName);
                     Assert.Equal(ExpectedPublicOrInternalProperties,mdType.Properties.Select(p=>p.Name));
                 })),
            };
            yield return new object[]
            {
                 new TestScenario("Local Public Class with internal properties",
                 typeof(Models.PublicWithInternalPropertiesClass),
                 new Action<Type,MetadataType>(static (clrType,mdType) =>
                 {
                     Assert.Equal(GetName(clrType), mdType.AssemblyQualifiedName);
                     Assert.Equal(ExpectedPublicOrInternalProperties,mdType.Properties.Select(p=>p.Name));
                 })),
            };
            // TestAssembly1 with InternalsVisibleTo
            yield return new object[]
            {
                 new TestScenario("InternalsVisibleTo Internal Attached Behavior",
                  typeof(A1::CompletionEngineTests.Models.InternalAttachedBehavior),
                 new Action<Type,MetadataType>(static (clrType,mdType) =>
                 {
                     Assert.Equal(GetName(clrType), mdType?.AssemblyQualifiedName);
                 })),
            };
            yield return new object[]
            {
                 new TestScenario("InternalsVisibleTo Internal Class",
                  typeof(A1::CompletionEngineTests.Models.InternalClass),
                 new Action<Type,MetadataType>(static (clrType,mdType) =>
                 {
                     Assert.Equal(GetName(clrType), mdType.AssemblyQualifiedName);
                     Assert.Equal(ExpectedPublicOrInternalProperties,mdType.Properties.Select(p=>p.Name));
                 })),
            };
            yield return new object[]
            {
                 new TestScenario("InternalsVisibleTo Public Class with internal properties",
                   typeof(A1::CompletionEngineTests.Models.PublicWithInternalPropertiesClass),
                 new Action<Type,MetadataType>(static (clrType,mdType) =>
                 {
                     Assert.Equal(GetName(clrType), mdType.AssemblyQualifiedName);
                     Assert.Equal(ExpectedPublicOrInternalProperties,mdType.Properties.Select(p=>p.Name));
                 })),
            };
            // TestAssembly2 without InternalsVisibleTo
            yield return new object[]
            {
                 new TestScenario("Not InternalsVisibleTo Internal Attached Behavior",
                 Type.GetType("CompletionEngineTests.Models.InternalAttachedBehavior, TestAssembly2"),
                 new Action<Type,MetadataType>(static (clrType,mdType) =>
                 {
                     Assert.Null(mdType);
                 })),
            };
            yield return new object[]
            {
                 new TestScenario("Not InternalsVisibleTo Internal Class",
                 Type.GetType("CompletionEngineTests.Models.InternalAttachedBehavior, TestAssembly2"),
                 new Action<Type,MetadataType>(static (clrType,mdType) =>
                 {
                     Assert.Null(mdType);
                 })),
            };
            yield return new object[]
            {
                 new TestScenario("InternalsVisibleTo Public Class with internal properties",
                   Type.GetType("CompletionEngineTests.Models.PublicWithInternalPropertiesClass, TestAssembly2"),
                 new Action<Type,MetadataType>(static (clrType,mdType) =>
                 {
                     Assert.Equal(GetName(clrType), mdType.AssemblyQualifiedName);
                     Assert.Equal(ExpectedPublicProperties,mdType.Properties.Select(p=>p.Name));
                 })),
            };
        }

        public record TestScenario(string Description, Type ClrType, Action<Type, MetadataType> CheckAction)
        {
            public override string ToString()
            {
                return Description;
            }
        }

        private static readonly string[] ExpectedPublicOrInternalProperties = new[]
        {
            nameof(Models.InternalClass.PublicProperty),
            nameof(Models.InternalClass.InternalProperty),
            nameof(Models.InternalClass.MixedInternalProperty),
        };

        private static readonly string[] ExpectedPublicProperties = new[]
{
            nameof(Models.InternalClass.PublicProperty),
        };

        private static Metadata Metadata = new MetadataReader(new DnlibMetadataProvider())
            .GetForTargetAssembly(new FolderAssemblyProvider(typeof(XamlCompletionTestBase).Assembly.GetModules()[0].FullyQualifiedName));

        private static string GetName(Type clrType) =>
            $"{clrType.FullName}, {clrType.Assembly.GetName().Name}";
    }
}
