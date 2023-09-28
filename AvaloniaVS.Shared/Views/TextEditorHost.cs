using System;
using System.Runtime.CompilerServices;
using AvaloniaVS.Models;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace AvaloniaVS.Shared.Views
{
    internal class TextEditorHost : IVsTextBufferDataEvents
    {
        private readonly IConnectionPoint _connectionPoint;
        private readonly uint _cookie;
        private Guid _xmlLanguageServiceGuid = new Guid("f6819a78-a205-47b5-be1c-675b3c7f0b8e");
        private readonly IVsTextLines _textLines;
        private readonly string _fileName;
        private readonly IComponentModel _componentModel;
        private IOleServiceProvider _oleServiceProvider;

        public TextEditorHost(IVsTextLines textLines, string fileName, IComponentModel componentModel, IOleServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _textLines = textLines;
            _fileName = fileName;
            _componentModel = componentModel;
            _oleServiceProvider = serviceProvider;

            // This allows us to subscribe to the "event" for when the text buffer is finally loaded
            // and ready to be used, COM event style
            var connectionPointContainer = textLines as IConnectionPointContainer;
            Guid bufferEventsGuid = typeof(IVsTextBufferDataEvents).GUID;
            connectionPointContainer.FindConnectionPoint(ref bufferEventsGuid, out _connectionPoint);
            _connectionPoint.Advise(this, out _cookie);
        }

        public IVsCodeWindow VsCodeWindow { get; private set; }

        public IWpfTextViewHost WpfTextViewHost { get; private set; }

        public IVsTextView WpfTextView { get; private set; }

        public string FileName => _fileName;

        public IVsTextLines TextBuffer => _textLines;

        public event EventHandler CodeWindowCreated;

        void IVsTextBufferDataEvents.OnFileChanged(uint grfChange, uint dwFileAttrs) { }

        public int OnLoadCompleted(int fReload)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // We no longer need to be notified, release this
            _connectionPoint.Unadvise(_cookie);

            // Set up the language service - this will activate intellisense and syntax highlighting
            _textLines.SetLanguageServiceID(ref _xmlLanguageServiceGuid);

            // Now we can create the IVsCodeWindow
            // If we don't wait until the text buffer is fully initialized before creating the IVsCodeWindow
            // it will fail completely and VS will abort loading our designer
            CreateCodeWindow();

            return VSConstants.S_OK;
        }

        private void CreateCodeWindow()
        {
            var eafs = _componentModel.GetService<IVsEditorAdaptersFactoryService>();

            var window = eafs.CreateVsCodeWindowAdapter(_oleServiceProvider);

            // Disable the splitter - which apparently causes a crash
            // amwx - not entirely sure if this is still the case, but I don't want to remove
            //        this just in case it is
            ((IVsCodeWindowEx)window).Initialize(
                (uint)_codewindowbehaviorflags.CWB_DISABLESPLITTER,
                VSUSERCONTEXTATTRIBUTEUSAGE.VSUC_Usage_Filter,
                szNameAuxUserContext: "",
                szValueAuxUserContext: "",
                InitViewFlags: 0,
                pInitView: new INITVIEW[1]);

            // Set the TextBuffer to the IVsCodeWindow
            ErrorHandler.ThrowOnFailure(window.SetBuffer(_textLines));

            // The following is why we wait until the TextBuffer is fully initialized. If we try
            // calling GetDocumentBuffer before the IVsTextBuffer is initialized, it will return 
            // null and that's no good
            // We need the buffer though, so we can set the property for XamlBufferMetadata
            // which associates it with Avalonia Xaml and stores the metadata for completion, etc
            var buffer = eafs.GetDocumentBuffer(_textLines);
            buffer.Properties.GetOrCreateSingletonProperty(() => new XamlBufferMetadata());

            // Get the view that the IVsCodeWindow hosts - so that we have a WPF control we 
            // can later insert into our designer control
            var primaryView = window.GetPrimaryView(out var ppView);
            var textViewHost = eafs.GetWpfTextViewHost(ppView);

            VsCodeWindow = window;
            WpfTextView = ppView;
            WpfTextViewHost = textViewHost;
            CodeWindowCreated?.Invoke(this, EventArgs.Empty);
        }
    }
}
