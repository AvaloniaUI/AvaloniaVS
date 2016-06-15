using System;

namespace AvaloniaVS.Internals
{
    internal static class Guids
    {
        // PamlEditorFactory guid
        public const string AvaloniaEditorFactoryString = "bd983d0e-8554-4053-a787-6f4872cf1d80";
        public static Guid AvaloniaEditorFactoryGuid = new Guid(AvaloniaEditorFactoryString);

        // xml language service id
        public const string XmlLanguageServiceString = @"{f6819a78-a205-47b5-be1c-675b3c7f0b8e}";
        public static Guid XmlLanguageServiceGuid = new Guid(XmlLanguageServiceString);

        // Avalonia designer general page guid
        public const string AvaloniaDesignerGeneralPageString = "b7e0e2c8-4fae-4387-8933-0560bee874c5";
        public static Guid AvaloniaDesignerGeneralPageGuid = new Guid(AvaloniaDesignerGeneralPageString);
    }
}