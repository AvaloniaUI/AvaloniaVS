using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using AvaloniaVS.Internals;

namespace AvaloniaVS.Helpers
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

        internal static T GetObjectSafe<T>(this EnvDTE.Project project) where T:class
        {
            try
            {
                return project as T ?? project?.Object as T;
            }
            catch (COMException e)
            {
                return null;
            }
        }
    }
}