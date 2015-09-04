//------------------------------------------------------------------------------
// <copyright file="XamlDocListenerTextViewCreationListener.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace PerspexVS
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class XamlDocListener : IWpfTextViewCreationListener
    {

        public void TextViewCreated(IWpfTextView textView)
        {
            ITextDocument document;
            var rc = textView.TextBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out document);
            if (rc != true) return;
            if (Path.GetFileName(document.FilePath.ToLower()).EndsWith(".xaml"))
            {
                new PerspexXamlDocDesigner(textView);
            }
        }
    }
}
