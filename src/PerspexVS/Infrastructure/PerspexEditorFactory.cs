using System;
using Microsoft.VisualStudio.Shell.Interop;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using PerspexVS.Internals;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace PerspexVS.Infrastructure
{
    [Guid(Guids.PerspexEditorFactoryString)]
    public class PerspexEditorFactory : IVsEditorFactory, IDisposable
    {
        private readonly PerspexPackage _package;
        private ServiceProvider _serviceProvider;
        private IOleServiceProvider _oleServiceProvider;

        public PerspexEditorFactory(PerspexPackage package)
        {
            _package = package;
        }

        public object GetService(Type serviceType)
        {
            return _serviceProvider.GetService(serviceType);
        }

        public int CreateEditorInstance(uint grfCreateDoc,
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
            ppunkDocView = IntPtr.Zero;
            ppunkDocData = IntPtr.Zero;
            pguidCmdUI = Guids.PerspexEditorFactoryGuid;
            pgrfCDW = 0;
            pbstrEditorCaption = null;

            // Validate inputs
            if ((grfCreateDoc & (VSConstants.CEF_OPENFILE | VSConstants.CEF_SILENT)) == 0)
            {
                return VSConstants.E_INVALIDARG;
            }

            IVsTextLines documentBuffer = null;

            if (punkDocDataExisting == IntPtr.Zero)
            {
                // the document is not yet open

                // create an invisible editor
                var invisibleEditorManager = (IVsInvisibleEditorManager)_serviceProvider.GetService(typeof(IVsInvisibleEditorManager));
                IVsInvisibleEditor invisibleEditor;
                ErrorHandler.ThrowOnFailure(invisibleEditorManager.RegisterInvisibleEditor(pszMkDocument,
                    pProject: null,
                    dwFlags: (uint)_EDITORREGFLAGS.RIEF_ENABLECACHING,
                    pFactory: null,
                    ppEditor: out invisibleEditor));

                var docDataPointer = IntPtr.Zero;
                var guidIVSTextLines = typeof(IVsTextLines).GUID;
                ErrorHandler.ThrowOnFailure(invisibleEditor.GetDocData(fEnsureWritable: 1, riid: ref guidIVSTextLines, ppDocData: out docDataPointer));
                documentBuffer = (IVsTextLines)Marshal.GetObjectForIUnknown(docDataPointer);

                //It is important to site the TextBuffer object
                var objWSite = documentBuffer as IObjectWithSite;
                if (objWSite != null)
                {
                    var oleServiceProvider = (IOleServiceProvider)GetService(typeof(IOleServiceProvider));
                    objWSite.SetSite(oleServiceProvider);
                }
            }
            else
            {
                documentBuffer = Marshal.GetObjectForIUnknown(punkDocDataExisting) as IVsTextLines;
                if (documentBuffer == null)
                {
                    return VSConstants.VS_E_INCOMPATIBLEDOCDATA;
                }
            }

            if (documentBuffer == null)
            {
                return VSConstants.S_FALSE;
            }

            // set xml as the language service id
            ErrorHandler.ThrowOnFailure(documentBuffer.SetLanguageServiceID(ref Guids.XmlLanguageServiceGuid));

            //Get the component model so we can request the editor adapter factory which we can use to spin up an editor instance.
            var componentModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
            var editorAdapterFactoryService = componentModel.GetService<IVsEditorAdaptersFactoryService>();

            //Create a code window adapter.
            var codeWindow = editorAdapterFactoryService.CreateVsCodeWindowAdapter(_oleServiceProvider);

            // Disable the splitter control on the editor as leaving it enabled causes a crash if the user
            // tries to use it here :(
            var codeWindowEx = (IVsCodeWindowEx)codeWindow;
            var initView = new INITVIEW[1];
            codeWindowEx.Initialize((uint)_codewindowbehaviorflags.CWB_DISABLESPLITTER,
                VSUSERCONTEXTATTRIBUTEUSAGE.VSUC_Usage_Filter,
                szNameAuxUserContext: "",
                szValueAuxUserContext: "",
                InitViewFlags: 0,
                pInitView: initView);

            //Associate our IVsTextLines with our new code window.
            ErrorHandler.ThrowOnFailure(codeWindow.SetBuffer(documentBuffer));

            //Get our text view for our editor which we will use to get the WPF control that hosts said editor.
            IVsTextView textView;
            ErrorHandler.ThrowOnFailure(codeWindow.GetPrimaryView(out textView));

            //Get our WPF host from our text view (from our code window).
            var textViewHost = editorAdapterFactoryService.GetWpfTextViewHost(textView);

            var editorPane = new PerspexDesignerPane(textViewHost, documentBuffer, textView, pszMkDocument);
            ppunkDocView = Marshal.GetIUnknownForObject(editorPane);
            ppunkDocData = Marshal.GetIUnknownForObject(documentBuffer);
            pbstrEditorCaption = "";
            return VSConstants.S_OK;
        }

        public int SetSite(IServiceProvider psp)
        {
            _oleServiceProvider = psp;
            _serviceProvider =new ServiceProvider(psp);
            return VSConstants.S_OK;
        }

        public int Close()
        {
            return VSConstants.S_OK;
        }

        public int MapLogicalView(ref Guid rguidLogicalView, out string pbstrPhysicalView)
        {
            pbstrPhysicalView = null;

            // primary view.
            if (rguidLogicalView == VSConstants.LOGVIEWID_Primary ||
                rguidLogicalView == VSConstants.LOGVIEWID_Code ||
                rguidLogicalView == VSConstants.LOGVIEWID_Debugging ||
                rguidLogicalView == VSConstants.LOGVIEWID_TextView ||
                rguidLogicalView == VSConstants.LOGVIEWID_Designer)
            {
                return VSConstants.S_OK;
            }

            return VSConstants.E_NOTIMPL;
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_serviceProvider != null)
                {
                    _serviceProvider.Dispose();
                    _serviceProvider = null;
                }
            }
        }
    }
}