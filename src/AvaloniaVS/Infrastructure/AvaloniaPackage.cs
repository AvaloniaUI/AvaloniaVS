//------------------------------------------------------------------------------
// <copyright file="AvaloniaPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using AvaloniaVS.Internals;
using AvaloniaVS.Options;
using System;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS.Infrastructure
{
    // this attribute will allow visual studio to look for assemblies in the extension
    // folder
    [ProvideBindingPath]

    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About

    [ProvideXmlEditorChooserDesignerView("Avalonia",
        "xaml",
        LogicalViewID.Designer,
        10000,
        Namespace = "https://github.com/avaloniaui",
        MatchExtensionAndNamespace = true,
        CodeLogicalViewEditor = typeof(AvaloniaEditorFactory),
        DesignerLogicalViewEditor = typeof(AvaloniaEditorFactory),
        DebuggingLogicalViewEditor = typeof(AvaloniaEditorFactory),
        TextLogicalViewEditor = typeof(AvaloniaEditorFactory))]

    [ProvideXmlEditorChooserDesignerView("Avalonia_Enforced",
        "xaml",
        LogicalViewID.Designer,
        10001,
        CodeLogicalViewEditor = typeof(AvaloniaEditorFactory),
        DesignerLogicalViewEditor = typeof(AvaloniaEditorFactory),
        DebuggingLogicalViewEditor = typeof(AvaloniaEditorFactory),
        TextLogicalViewEditor = typeof(AvaloniaEditorFactory))]

    [ProvideEditorExtension(typeof(AvaloniaEditorFactory), ".paml", 100, NameResourceID = 113, DefaultName = "Avalonia Xaml Editor")]
    [ProvideEditorLogicalView(typeof(AvaloniaEditorFactory), LogicalViewID.TextView)]
    [ProvideEditorLogicalView(typeof(AvaloniaEditorFactory), LogicalViewID.Code)]
    [ProvideEditorLogicalView(typeof(AvaloniaEditorFactory), LogicalViewID.Designer)]
    [ProvideEditorLogicalView(typeof(AvaloniaEditorFactory), LogicalViewID.Debugging)]

    [ProvideEditorFactory(typeof(AvaloniaEditorFactory),
        113,
        TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]

    // Options pages
    [ProvideProfile(typeof(AvaloniaDesignerGeneralPage), "Avalonia designer", "Avalonia Designer Options", 114, 114, true, DescriptionResourceID = 114)]
    [ProvideOptionPage(typeof(AvaloniaDesignerGeneralPage),
        "Avalonia Designer",
        "General",
        114,
        115,
        true,
        new[] { "xaml", "designer" })]

    // we let the shell know that the package exposes some menus
    [ProvideMenuResource("Menus.ctmenu", 1)]

    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideAutoLoad("F1536EF8-92EC-443C-9ED7-FDADF150DA82")]
    [ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F")]
    [Guid(PackageGuidString)]
    [ComVisible(true)]
    [Export]
    public sealed class AvaloniaPackage : AsyncPackage
    {
        public const string PackageGuidString = "865ba8d5-1180-4bf8-8821-345f72a4cb79";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            await InitializeVisualStudioServices();

            var pamlEditorFactory = VisualStudioServices.ComponentModel.DefaultExportProvider.GetExportedValue<AvaloniaEditorFactory>();
            base.RegisterEditorFactory(pamlEditorFactory);
        }

        private async Task InitializeVisualStudioServices()
        {
            var componentModel = (IComponentModel)(await GetServiceAsync(typeof(SComponentModel)));
            VisualStudioServices.ComponentModel = componentModel;
            VisualStudioServices.VsEditorAdaptersFactoryService = componentModel.GetService<IVsEditorAdaptersFactoryService>();
        }
    }
}