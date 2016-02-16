using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

namespace PerspexVS.Helpers
{
    internal static class Extensions
    {
        internal static WritableSettingsStore GetWritableSettingsStore(this SVsServiceProvider vsServiceProvider, SettingsScope scope)
        {
            var shellSettingsManager = new ShellSettingsManager(vsServiceProvider);
            return shellSettingsManager.GetWritableSettingsStore(scope);
        }

    }
}