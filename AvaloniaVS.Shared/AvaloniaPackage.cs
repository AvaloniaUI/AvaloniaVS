using System;
using System.Runtime.InteropServices;
using System.Threading;
using AvaloniaVS.Services;
using AvaloniaVS.Views;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Serilog;
using Serilog.Core;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS
{
    [Guid(Constants.PackageGuidString)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideEditorExtension(
        typeof(EditorFactory),
        "." + Constants.axaml,
        100,
        NameResourceID = 113,
        EditorFactoryNotify = true,
        ProjectGuid = VSConstants.UICONTEXT.CSharpProject_string,
        DefaultName = Constants.PackageName)]
    [ProvideEditorExtension(
        typeof(EditorFactory),
        "." + Constants.paml,
        100,
        NameResourceID = 113,
        EditorFactoryNotify = true,
        ProjectGuid = VSConstants.UICONTEXT.CSharpProject_string,
        DefaultName = Constants.PackageName)]
    [ProvideEditorExtension(
        typeof(EditorFactory),
        "." + Constants.xaml,
        0x40,
        NameResourceID = 113,
        EditorFactoryNotify = true,
        ProjectGuid = VSConstants.UICONTEXT.CSharpProject_string,
        DefaultName = Constants.PackageName)]
    [ProvideEditorFactory(typeof(EditorFactory), 113, TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]
    [ProvideEditorLogicalView(typeof(EditorFactory), LogicalViewID.Designer)]
    [ProvideXmlEditorChooserDesignerView(Constants.PackageName,
        Constants.xaml,
        LogicalViewID.Designer,
        10001,
        Namespace = "https://github.com/avaloniaui",
        MatchExtensionAndNamespace = false,
        CodeLogicalViewEditor = typeof(EditorFactory),
        DesignerLogicalViewEditor = typeof(EditorFactory),
        DebuggingLogicalViewEditor = typeof(EditorFactory),
        TextLogicalViewEditor = typeof(EditorFactory))]
    [ProvideXmlEditorChooserDesignerView(Constants.PackageName,
        Constants.axaml,
        LogicalViewID.Designer,
        10000,
        Namespace = "https://github.com/avaloniaui",
        MatchExtensionAndNamespace = false,
        CodeLogicalViewEditor = typeof(EditorFactory),
        DesignerLogicalViewEditor = typeof(EditorFactory),
        DebuggingLogicalViewEditor = typeof(EditorFactory),
        TextLogicalViewEditor = typeof(EditorFactory))]
    [ProvideOptionPage(typeof(OptionsDialogPage), Constants.PackageName, "General", 113, 0, supportsAutomation: true)]
    [ProvideBindingPath]
    internal sealed class AvaloniaPackage : AsyncPackage
    {
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
            var settings = this.GetMefService<IAvaloniaVSSettings>();
            var levelSwitch = new LoggingLevelSwitch() { MinimumLevel = settings.MinimumLogVerbosity };

            settings.PropertyChanged += (s, e) => levelSwitch.MinimumLevel = settings.MinimumLogVerbosity;

            var sink = new OutputPaneEventSink(ouput, outputTemplate: format);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Sink(sink, levelSwitch: levelSwitch)
                .WriteTo.Trace(outputTemplate: format)
                .CreateLogger();
        }
    }
}
