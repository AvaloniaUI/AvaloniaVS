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
    }
}