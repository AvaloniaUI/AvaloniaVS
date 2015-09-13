//------------------------------------------------------------------------------
// <copyright file="PerspexEditorMarginFactory.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace PerspexVS
{
    /// <summary>
    /// Export a <see cref="IWpfTextViewMarginProvider"/>, which returns an instance of the margin for the editor to use.
    /// </summary>
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name("PerspexDesigner")]
    [Order(After = PredefinedMarginNames.HorizontalScrollBar)]  // Ensure that the margin occurs below the horizontal scrollbar
    [MarginContainer(PredefinedMarginNames.Top)]             // Set the container to the bottom of the editor window
    [ContentType("text")]                                       // Show this margin for all text-based types
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class PerspexEditorMarginFactory : IWpfTextViewMarginProvider
    {
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            
            var file = wpfTextViewHost.TextView.GetFilePath()?.ToLower();
            bool edit = file?.EndsWith(".paml") == true;
            if (!edit && file?.EndsWith(".xaml") == true)
            {
                edit = Utils.CheckPerspexRoot(File.ReadAllText(file));
            }
            if (edit)
                return new PerspexEditorMargin(wpfTextViewHost.TextView);
            return null;
        }
    }
}
