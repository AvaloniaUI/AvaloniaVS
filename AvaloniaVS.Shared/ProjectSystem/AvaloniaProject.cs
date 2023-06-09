using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS.ProjectSystem;

[Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
[AppliesTo(Constants.AvaloniaCapability)]
internal class AvaloniaProject : IProjectDynamicLoadComponent
{
    private IAsyncServiceProvider asyncServiceProvider;

    public async Task LoadAsync()
    {
        if (asyncServiceProvider is null)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (ServiceProvider.GlobalProvider.GetService(typeof(IVsShell)) is IVsShell shell)
            {
                if (shell.IsPackageLoaded(Constants.PackageGuid, out var vsPackage)
                   != Microsoft.VisualStudio.VSConstants.S_OK)
                {
                    shell.LoadPackage(Constants.PackageGuid, out vsPackage);
                }
                asyncServiceProvider = (IAsyncServiceProvider)vsPackage;
            }
        }
    }

    public async Task UnloadAsync()
    {
        // Unload the feature
        await Task.CompletedTask;
    }
}
