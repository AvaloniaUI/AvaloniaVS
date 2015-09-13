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
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(PerspexPackage.PackageGuidString)]

    //Register these types for default XML editor
    [ProvideXmlEditorChooserDesignerView("Perspex", ".xaml", LogicalViewID.Designer, 9001,
        DesignerLogicalViewEditor ="FA3CD31E-987B-443A-9B81-186104E8DAC1",
        Namespace = "https://github.com/grokys/perspex",
        MatchExtensionAndNamespace = true)]
    [ProvideXmlEditorChooserDesignerView("Perspex", ".paml", LogicalViewID.Designer, 9001,
        DesignerLogicalViewEditor = "FA3CD31E-987B-443A-9B81-186104E8DAC1")]


    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideAutoLoad("F1536EF8-92EC-443C-9ED7-FDADF150DA82")]
    [ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F")]
    public sealed class PerspexPackage : Package
    {
        public const string PackageGuidString = "865ba8d5-1180-4bf8-8821-345f72a4cb79";
                
    }
}
