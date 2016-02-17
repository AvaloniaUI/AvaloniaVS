using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using PerspexVS.Internals;

namespace PerspexVS.Helpers
{
    internal static class Extensions
    {
        internal static WritableSettingsStore GetWritableSettingsStore(this SVsServiceProvider vsServiceProvider, SettingsScope scope)
        {
            var shellSettingsManager = new ShellSettingsManager(vsServiceProvider);
            return shellSettingsManager.GetWritableSettingsStore(scope);
        }

        internal static ITextBuffer GetTextBuffer(this IVsTextLines vsTextBuffer)
        {
            return VisualStudioServices.VsEditorAdaptersFactoryService.GetDataBuffer(vsTextBuffer);
        }
    }
}