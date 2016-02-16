using System;

namespace PerspexVS.Internals
{
    internal static class Guids
    {
        // PamlEditorFactory guid
        public const string PerspexEditorFactoryString = "bd983d0e-8554-4053-a787-6f4872cf1d80";
        public static Guid PerspexEditorFactoryGuid = new Guid(PerspexEditorFactoryString);

        // xml language service id
        public const string XmlLanguageServiceString = @"{f6819a78-a205-47b5-be1c-675b3c7f0b8e}";
        public static Guid XmlLanguageServiceGuid = new Guid(XmlLanguageServiceString);

        // paml designer general page guid
        public const string PerspexDesignerGeneralPageString = "b7e0e2c8-4fae-4387-8933-0560bee874c5";
        public static Guid PerspexDesignerGeneralPageGuid = new Guid(PerspexDesignerGeneralPageString);
    }
}