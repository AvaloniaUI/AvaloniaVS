using System;

namespace PerspexVS.Internals
{
    internal static class Guids
    {
        // PamlEditorFactory guid
        public const string PamlDesignerEditorFactoryString = "bd983d0e-8554-4053-a787-6f4872cf1d80";
        public static Guid PamlDesignerEditorFactoryGuid = new Guid(PamlDesignerEditorFactoryString);

        // xml editor guid
        public const string XmlChooserEditorFactoryGuid = @"{32CC8DFA-2D70-49b2-94CD-22D57349B778}";

        // xml language service id
        public const string XmlLanguageServiceString = @"{f6819a78-a205-47b5-be1c-675b3c7f0b8e}";
        public static Guid XmlLanguageServiceGuid = new Guid(XmlLanguageServiceString);
    }
}