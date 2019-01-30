using System;
using System.Runtime.InteropServices;
using AvaloniaVS.Models;
using AvaloniaVS.Views;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace AvaloniaVS.Services
{
    public sealed class EditorFactory : IVsEditorFactory, IDisposable
    {
        const int CLSCTX_INPROC_SERVER = 1;
        readonly AvaloniaPackage _package;
        IOleServiceProvider _oleServiceProvider;
        ServiceProvider _serviceProvider;

        public EditorFactory(AvaloniaPackage package) => _package = package;

        public int SetSite(IOleServiceProvider psp)
        {
            _oleServiceProvider = psp;
            _serviceProvider = new ServiceProvider(psp);
            return VSConstants.S_OK;
        }

        public int MapLogicalView(ref Guid rguidLogicalView, out string pbstrPhysicalView)
        {
            pbstrPhysicalView = null;
            return rguidLogicalView == VSConstants.LOGVIEWID_Primary ?
                VSConstants.S_OK : VSConstants.E_NOTIMPL;
        }

        public int CreateEditorInstance(
            uint grfCreateDoc,
            string pszMkDocument,
            string pszPhysicalView,
            IVsHierarchy pvHier,
            uint itemid,
            IntPtr punkDocDataExisting,
            out IntPtr ppunkDocView,
            out IntPtr ppunkDocData,
            out string pbstrEditorCaption,
            out Guid pguidCmdUI,
            out int pgrfCDW)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ppunkDocView = IntPtr.Zero;
            ppunkDocData = IntPtr.Zero;
            pguidCmdUI = Guids.AvaloniaDesignerEditorFactory;
            pgrfCDW = 0;
            pbstrEditorCaption = string.Empty;

            if ((grfCreateDoc & (VSConstants.CEF_OPENFILE | VSConstants.CEF_SILENT)) == 0)
            {
                return VSConstants.E_INVALIDARG;
            }

            var textBuffer = GetTextBuffer(pszMkDocument, punkDocDataExisting);
            var (editorWindow, editorControl) = CreateEditorControl(textBuffer);
            var pane = new DesignerPane(pszMkDocument, editorWindow, editorControl);
            ppunkDocView = Marshal.GetIUnknownForObject(pane);
            ppunkDocData = Marshal.GetIUnknownForObject(textBuffer);
            return VSConstants.S_OK;
        }

        public int Close() => VSConstants.S_OK;

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }

        private IVsTextLines GetTextBuffer(string fileName, IntPtr punkDocDataExisting)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsTextLines result;

            if (punkDocDataExisting == IntPtr.Zero)
            {
                // Get an invisible editor over the file. This is much easier than having to
                // manually figure out the right content type and language service, and it will
                // automatically associate the document with its owning project, meaning we will
                // get intellisense in our editor with no extra work.
                var iem = _serviceProvider.GetService<IVsInvisibleEditorManager, SVsInvisibleEditorManager>();

                ErrorHandler.ThrowOnFailure(iem.RegisterInvisibleEditor(
                    fileName,
                    pProject: null,
                    dwFlags: (uint)_EDITORREGFLAGS.RIEF_ENABLECACHING,
                    pFactory: null,
                    ppEditor: out var invisibleEditor));

                var guidIVSTextLines = typeof(IVsTextLines).GUID;
                ErrorHandler.ThrowOnFailure(invisibleEditor.GetDocData(
                    fEnsureWritable: 1,
                    riid: ref guidIVSTextLines,
                    ppDocData: out var docDataPointer));

                result = (IVsTextLines)Marshal.GetObjectForIUnknown(docDataPointer);
            }
            else
            {
                result = Marshal.GetObjectForIUnknown(punkDocDataExisting) as IVsTextLines;

                if (result == null)
                {
                    ErrorHandler.ThrowOnFailure(VSConstants.VS_E_INCOMPATIBLEDOCDATA);
                }
            }

            return result;
        }

        private (IVsCodeWindow, IWpfTextViewHost) CreateEditorControl(IVsTextLines bufferAdapter)
        {
            var componentModel = _serviceProvider.GetService<IComponentModel, SComponentModel>();
            var eafs = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            var codeWindow = eafs.CreateVsCodeWindowAdapter(_oleServiceProvider);

            // Disable the splitter control on the editor as leaving it enabled causes a crash if the user
            // tries to use it here.
            ((IVsCodeWindowEx)codeWindow).Initialize(
                (uint)_codewindowbehaviorflags.CWB_DISABLESPLITTER,
                VSUSERCONTEXTATTRIBUTEUSAGE.VSUC_Usage_Filter,
                szNameAuxUserContext: "",
                szValueAuxUserContext: "",
                InitViewFlags: 0,
                pInitView: new INITVIEW[1]);

            // Add metadata to the buffer so we can identify it as containing Avalonia XAML.
            var buffer = eafs.GetDataBuffer(bufferAdapter);
            buffer.Properties.GetOrCreateSingletonProperty(() => new XamlBufferMetadata());

            codeWindow.SetBuffer(bufferAdapter);
            ErrorHandler.ThrowOnFailure(codeWindow.GetPrimaryView(out var textView));
            return (codeWindow, eafs.GetWpfTextViewHost(textView));
        }
    }
}
