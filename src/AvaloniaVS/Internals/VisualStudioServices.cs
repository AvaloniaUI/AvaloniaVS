using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;

namespace AvaloniaVS.Internals
{
    internal static class VisualStudioServices
    {
        public static IComponentModel ComponentModel { get; set; }

        public static IVsEditorAdaptersFactoryService VsEditorAdaptersFactoryService { get; set; }
    }
}