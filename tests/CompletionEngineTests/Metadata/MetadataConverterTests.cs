extern alias A1;
extern alias A2;

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Ide.CompletionEngine;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;
using Xunit;

namespace CompletionEngineTests
{
    public class MetadataConverterTests
    {
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
            Assert.Equal(clrType.AssemblyQualifiedName, type.AssemblyQualifiedName);



            Type clrTypeA1 = typeof(A1::CompletionEngineTests.Models.AttachedBehavior);
            nsName = "clr-namespace:" + clrTypeA1.Namespace + ";assembly=" + clrTypeA1.Assembly.GetName().Name;

            ns = Metadata.Namespaces[nsName];

            Assert.NotNull(ns);

            MetadataType typeA1 = ns[clrTypeA1.Name];

            Assert.NotNull(typeA1);

            Assert.Equal(clrTypeA1.AssemblyQualifiedName, typeA1.AssemblyQualifiedName);


            Type clrTypeA2 = typeof(A2::CompletionEngineTests.Models.AttachedBehavior);
            nsName = "clr-namespace:" + clrTypeA1.Namespace + ";assembly=" + clrTypeA2.Assembly.GetName().Name;

            ns = Metadata.Namespaces[nsName];

            Assert.NotNull(ns);

            MetadataType typeA2 = ns[clrTypeA2.Name];

            Assert.NotNull(typeA2);

            Assert.Equal(clrTypeA2.AssemblyQualifiedName, typeA2.AssemblyQualifiedName);
        }


        [Fact]
        public void Discover_InternalsVisibleTo()
        {
            // Check local 
            Type clrType = typeof(Models.InternalAttachedBehavior);
            string nsName = "clr-namespace:" + clrType.Namespace + ";assembly=" + clrType.Assembly.GetName().Name;
            Dictionary<string, MetadataType> ns = Metadata.Namespaces[nsName];

            Assert.NotNull(ns);
            ns.TryGetValue(clrType.Name, out var type);
            Assert.NotNull(type);
            Assert.Equal(clrType.AssemblyQualifiedName, type.AssemblyQualifiedName);

            Type clrTypeA1 = typeof(A1::CompletionEngineTests.Models.InternalAttachedBehavior);
            nsName = "clr-namespace:" + clrTypeA1.Namespace + ";assembly=" + clrTypeA1.Assembly.GetName().Name;

            ns = Metadata.Namespaces[nsName];

            Assert.NotNull(ns);

            ns.TryGetValue(clrTypeA1.Name, out var typeA1);

            Assert.NotNull(typeA1);

            Assert.Equal(clrTypeA1.AssemblyQualifiedName, typeA1.AssemblyQualifiedName);


            Type clrTypeA2 = Type.GetType("CompletionEngineTests.Models.InternalAttachedBehavior, Ass2, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            nsName = "clr-namespace:" + clrTypeA1.Namespace + ";assembly=" + clrTypeA2.Assembly.GetName().Name;

            ns = Metadata.Namespaces[nsName];

            Assert.NotNull(ns);

            ns.TryGetValue(clrTypeA2.Name, out var typeA2);

            Assert.Null(typeA2);
            
        }

        private static Metadata Metadata = new MetadataReader(new DnlibMetadataProvider())
            .GetForTargetAssembly(typeof(XamlCompletionTestBase).Assembly.GetModules()[0].FullyQualifiedName);
    }
}
