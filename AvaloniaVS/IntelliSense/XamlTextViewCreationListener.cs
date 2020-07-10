using System;
using System.ComponentModel.Composition;
using AvaloniaVS.Models;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace AvaloniaVS.IntelliSense
{
    /// <summary>
    /// Registers a <see cref="XamlCompletionCommandHandler"/> with newly-created text views.
    /// </summary>
    [Name("Avalonia XAML manupulator")]
    [ContentType("xml")]
    [Export(typeof(IWpfTextViewCreationListener))]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class XamlTextViewCreationListener : IWpfTextViewCreationListener


    {
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public XamlTextViewCreationListener(
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void TextViewCreated(IWpfTextView textView)
        {            
            // If the buffer contains Avalonia XAML, register a completion handler on it.
            if (textView.TextBuffer.Properties.ContainsProperty(typeof(XamlBufferMetadata)))
            {
                new XamlTextManipulatorRegistrar(textView);
            }
        }
    }
}
