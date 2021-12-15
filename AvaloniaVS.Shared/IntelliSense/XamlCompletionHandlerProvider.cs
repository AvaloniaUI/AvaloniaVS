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
    [Export(typeof(IVsTextViewCreationListener))]
    [Name("Avalonia XAML completion handler")]
    [ContentType("xml")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class XamlCompletionHandlerProvider : IVsTextViewCreationListener
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IVsEditorAdaptersFactoryService _adapterService;
        private readonly ICompletionBroker _completionBroker;

        [ImportingConstructor]
        public XamlCompletionHandlerProvider(
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
            IVsEditorAdaptersFactoryService adapterService,
            ICompletionBroker completionBroker)
        {
            _serviceProvider = serviceProvider;
            _adapterService = adapterService;
            _completionBroker = completionBroker;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var textView = _adapterService.GetWpfTextView(textViewAdapter);

            // If the buffer contains Avalonia XAML, register a completion handler on it.
            if (textView.TextBuffer.Properties.ContainsProperty(typeof(XamlBufferMetadata)))
            {
                textView.Properties.GetOrCreateSingletonProperty(
                    () => new XamlCompletionCommandHandler(
                        _serviceProvider,
                        _completionBroker,
                        textView,
                        textViewAdapter));
            }
        }
    }
}
