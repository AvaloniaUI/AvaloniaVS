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

        private static Metadata Metadata = new MetadataReader(new DnlibMetadataProvider())
            .GetForTargetAssembly(typeof(XamlCompletionTestBase).Assembly.GetModules()[0].FullyQualifiedName);
    }
}
