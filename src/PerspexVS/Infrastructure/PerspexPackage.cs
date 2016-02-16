//------------------------------------------------------------------------------
// <copyright file="PerspexPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace PerspexVS.Infrastructure
{
    // this attribute will allow visual studio to look for assemblies in the extension
    // folder
    [ProvideBindingPath]

    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About

    [ProvideXmlEditorChooserDesignerView("Perspex",
        "xaml",
        LogicalViewID.Designer,
        1000,
        Namespace = "https://github.com/perspex",
        MatchExtensionAndNamespace = true,
        CodeLogicalViewEditor = typeof(PerspexEditorFactory),
        DesignerLogicalViewEditor = typeof(PerspexEditorFactory),
        DebuggingLogicalViewEditor = typeof(PerspexEditorFactory),
        TextLogicalViewEditor = typeof(PerspexEditorFactory))]


    [ProvideEditorFactory(typeof(PerspexEditorFactory),
        113,
        TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]

    // we let the shell know that the package exposes some menus
    [ProvideMenuResource("Menus.ctmenu", 1)]

    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideAutoLoad("F1536EF8-92EC-443C-9ED7-FDADF150DA82")]
    [ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F")]
    [Guid(PackageGuidString)]
    [ComVisible(true)]
    public sealed class PerspexPackage : Package
    {
        public const string PackageGuidString = "865ba8d5-1180-4bf8-8821-345f72a4cb79";

        protected override void Initialize()
        {
            base.Initialize();
            base.RegisterEditorFactory(new PerspexEditorFactory(this));
        }
    }
}