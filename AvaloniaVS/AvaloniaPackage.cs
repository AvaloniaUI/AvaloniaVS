using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Remote.Protocol.Designer;
using AvaloniaVS.Services;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Serilog;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS
{
    [Guid(PackageGuidString)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideEditorExtension(typeof(EditorFactory), ".paml", 100, NameResourceID = 113, DefaultName = "Avalonia Xaml Editor")]
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
    internal sealed class AvaloniaPackage : AsyncPackage
    {
        public const string PackageGuidString = "865ba8d5-1180-4bf8-8821-345f72a4cb79";

        public static SolutionService SolutionService { get; private set; }

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            InitializeLogging();
            RegisterEditorFactory(new EditorFactory(this));

            var dte = (DTE)await GetServiceAsync(typeof(DTE));
            SolutionService = new SolutionService(dte);

            Log.Logger.Information("Avalonia Package initialized");
        }

        private void InitializeLogging()
        {
            const string format = "{Timestamp:HH:mm:ss.fff} [{Level}] {Pid} {Message}{NewLine}{Exception}";
            var ouput = this.GetService<IVsOutputWindow, SVsOutputWindow>();
            var sink = new OutputPaneEventSink(ouput, outputTemplate: format);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(sink)
                .WriteTo.Trace(outputTemplate: format)
                .CreateLogger();
        }

        private object TransformUpdateXamlMessage(UpdateXamlMessage m)
        {
            return new
            {
                m.AssemblyPath,
                m.XamlFileProjectPath,
                XamlLength = m.Xaml.Length,
            };
        }
    }
}
