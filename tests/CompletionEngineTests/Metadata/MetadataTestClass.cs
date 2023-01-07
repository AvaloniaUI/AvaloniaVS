using System;
using Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;
using Avalonia.Interactivity;

namespace CompletionEngineTests
{
    /// <summary>
    /// This class should not have Event in name
    /// as it is used to check if discovery of attached events is working,
    /// see <see cref="FieldWrapper"/>
    /// </summary>
    public class MetadataTestClass : RoutedEvent
    {
        /// <summary>
        /// Field which should be recognized as attached event,
        /// as its declaring type is sublcass of <see cref="RoutedEvent"/>
        /// </summary>
        public static MetadataTestClass field;

        public MetadataTestClass(string name, RoutingStrategies routingStrategies, Type eventArgsType, Type ownerType) : base(name, routingStrategies, eventArgsType, ownerType)
        {
        }
    }
}
