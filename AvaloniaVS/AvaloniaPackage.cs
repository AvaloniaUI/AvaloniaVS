using System;
using System.Runtime.InteropServices;
using System.Threading;
using AvaloniaVS.Services;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS
{
    [Guid(PackageGuidString)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideEditorFactory(typeof(EditorFactory), 113, TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]
    [ProvideEditorLogicalView(typeof(EditorFactory), LogicalViewID.Designer)]
    [ProvideXmlEditorChooserDesignerView("Avalonia",
        "xaml",
        LogicalViewID.Designer,
        10000,
        Namespace = "https://github.com/avaloniaui",
        MatchExtensionAndNamespace = true,
        CodeLogicalViewEditor = typeof(EditorFactory),
        DesignerLogicalViewEditor = typeof(EditorFactory),
        DebuggingLogicalViewEditor = typeof(EditorFactory),
        TextLogicalViewEditor = typeof(EditorFactory))]
    public sealed class AvaloniaPackage : AsyncPackage
    {
        public const string PackageGuidString = "894B5FA9-7669-4E8A-81DE-709F18B47CEE";

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RegisterEditorFactory(new EditorFactory(this));
        }
    }
}
