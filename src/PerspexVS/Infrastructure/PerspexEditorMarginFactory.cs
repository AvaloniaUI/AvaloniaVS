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
using Perspex.Designer;

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
            if (Utils.IsPerspexMarkup(wpfTextViewHost.TextView))
            {
                var editor = new PerspexEditorMargin(wpfTextViewHost.TextView);
                wpfTextViewHost.TextView.TextBuffer.Properties[typeof (PerspexDesigner)] = editor.Designer;
                return editor;
            }
            return null;
        }
    }
}
