using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace PerspexVS
{
    /// <summary>
    /// Listens to the creation of all textview, but only applies the Perspex designer to xaml views
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class XamlDocListener : IWpfTextViewCreationListener
    {
        public static XamlDocListener Instance { get; private set; }

        public List<PerspexXamlDocDesigner> PerspexXamlDocDesigners { get; }

        public static PerspexBuildEvents Events { get; private set; }

        /// <summary>
        /// Creates the static instance, create designer list
        /// (may need later), finally sets up build events each
        /// <see cref="PerspexXamlDocDesigner"/> will subscribe to.
        /// </summary>
        public XamlDocListener()
        {
            Instance = this;
            PerspexXamlDocDesigners = new List<PerspexXamlDocDesigner>();
            Events = new PerspexBuildEvents();
        }

        /// <summary>
        /// Detects if the IWpfTextView's Document Extensions is .xaml and creates 
        /// a perspex designer for it if true.
        /// </summary>
        /// <param name="textView"></param>
        public void TextViewCreated(IWpfTextView textView)
        {
            ITextDocument document;
            var rc = textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document);
            if (rc != true) return;
            var path = document.FilePath;
            if (!Path.GetFileName(path.ToLower()).EndsWith(".xaml")) return;
            var designer = new PerspexXamlDocDesigner(textView, path);
            PerspexXamlDocDesigners.Add(designer);
        }
    }
}
