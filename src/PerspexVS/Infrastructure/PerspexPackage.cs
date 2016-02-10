//------------------------------------------------------------------------------
// <copyright file="PerspexPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using PerspexVS.Internals;

namespace PerspexVS.Infrastructure
{
    // this attribute will allow visual studio to look for assemblies in the extension
    // folder
    [ProvideBindingPath]

    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About

    //Register these types for default XML editor
    [ProvideXmlEditorChooserDesignerView("Perspex",
        ".paml",
        LogicalViewID.Designer,
        0x60,
        DesignerLogicalViewEditor = typeof(PamlEditorFactory),
        Namespace = "https://github.com/grokys/perspex",
        MatchExtensionAndNamespace = true)]

    [ProvideEditorFactory(typeof(PamlEditorFactory),
        113,
        TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]


    [ProvideEditorExtension(typeof(PamlEditorFactory),
        PamlEditorFactory.Extension,
        100,
        NameResourceID = 113)]
    [ProvideEditorLogicalView(typeof(PamlEditorFactory), LogicalViewID.Designer)]

    // we let the shell know that the package exposes some menus
    [ProvideMenuResource(1000, 1)]

    // We register the XML Editor ("{FA3CD31E-987B-443A-9B81-186104E8DAC1}") as an EditorFactoryNotify
    // object to handle our ".vstemplate" file extension for the following projects:
    // Microsoft Visual Basic Project
    [EditorFactoryNotifyForProject("{F184B08F-C81C-45F6-A57F-5ABD9991F28F}", PamlEditorFactory.Extension, Guids.XmlChooserEditorFactoryGuid)]
    // Microsoft Visual C# Project
    [EditorFactoryNotifyForProject("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", PamlEditorFactory.Extension, Guids.XmlChooserEditorFactoryGuid)]

    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideAutoLoad("F1536EF8-92EC-443C-9ED7-FDADF150DA82")]
    [ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F")]
    [Guid(PerspexPackage.PackageGuidString)]
    [ComVisible(true)]
    public sealed class PerspexPackage : Package
    {
        public const string PackageGuidString = "865ba8d5-1180-4bf8-8821-345f72a4cb79";

        protected override void Initialize()
        {
            base.Initialize();
            base.RegisterEditorFactory(new PamlEditorFactory(this));
        }
    }
}