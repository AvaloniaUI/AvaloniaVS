using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.InteropServices;
using AvaloniaVS.Internals;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace AvaloniaVS.Infrastructure
{
    [Guid(Guids.AvaloniaEditorFactoryString), Export]
    public class AvaloniaEditorFactory : IVsEditorFactory, IDisposable
    {
        private readonly AvaloniaPackage _package;
        private readonly IAvaloniaDesignerSettings _designerSettings;
        private ServiceProvider _serviceProvider;
        private IOleServiceProvider _oleServiceProvider;

        [ImportingConstructor]
        public AvaloniaEditorFactory(AvaloniaPackage package, IAvaloniaDesignerSettings designerSettings)
        {
            _package = package;
            _designerSettings = designerSettings;
        }

        public object GetService(Type serviceType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
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
            ThreadHelper.ThrowIfNotOnUIThread();
            ppunkDocView = IntPtr.Zero;
            ppunkDocData = IntPtr.Zero;
            pguidCmdUI = Guids.AvaloniaDesignerGeneralPageGuid;
            pgrfCDW = 0;
            pbstrEditorCaption = null;

            // Validate inputs
            if ((grfCreateDoc & (VSConstants.CEF_OPENFILE | VSConstants.CEF_SILENT)) == 0) {
                return VSConstants.E_INVALIDARG;
            }

            if (pszMkDocument.ToLowerInvariant().EndsWith(".xaml")) {
                //Special handling for xaml documents
                if (!Utils.CheckAvaloniaRoot(File.ReadAllText(pszMkDocument))) {
                    return VSConstants.VS_E_UNSUPPORTEDFORMAT;
                }
            }

            IVsTextLines documentBuffer = null;

            if (punkDocDataExisting == IntPtr.Zero) {
                // create an invisible editor
                var invisibleEditorManager = (IVsInvisibleEditorManager)_serviceProvider.GetService(typeof(IVsInvisibleEditorManager));
                Assumes.Present(invisibleEditorManager);
                ErrorHandler.ThrowOnFailure(invisibleEditorManager.RegisterInvisibleEditor(pszMkDocument,
                    null,
                    (uint)_EDITORREGFLAGS.RIEF_ENABLECACHING,
                    null,
                    out var invisibleEditor));

                var docDataPointer = IntPtr.Zero;
                var guidIVSTextLines = typeof(IVsTextLines).GUID;
                ErrorHandler.ThrowOnFailure(invisibleEditor.GetDocData(1, ref guidIVSTextLines, out docDataPointer));
                documentBuffer = (IVsTextLines)Marshal.GetObjectForIUnknown(docDataPointer);

                //It is important to site the TextBuffer object
                if (documentBuffer is IObjectWithSite objWSite) {
                    var oleServiceProvider = (IOleServiceProvider)GetService(typeof(IOleServiceProvider));
                    objWSite.SetSite(oleServiceProvider);
                }
            } else {
                documentBuffer = Marshal.GetObjectForIUnknown(punkDocDataExisting) as IVsTextLines;
                if (documentBuffer == null) {
                    return VSConstants.VS_E_INCOMPATIBLEDOCDATA;
                }
            }

            if (documentBuffer == null) {
                return VSConstants.S_FALSE;
            }

            // set xml as the language service id
            ErrorHandler.ThrowOnFailure(documentBuffer.SetLanguageServiceID(ref Guids.XmlLanguageServiceGuid));

            var editorPane = GetDocumentView(documentBuffer, pszMkDocument);
            ppunkDocView = Marshal.GetIUnknownForObject(editorPane);
            ppunkDocData = Marshal.GetIUnknownForObject(documentBuffer);
            pbstrEditorCaption = "";
            return VSConstants.S_OK;
        }

        internal object GetDocumentView(IVsTextLines documentBuffer, string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var codeWindow = CreateVsCodeWindow(documentBuffer);

            // in case the designer is not supported, we return the current VsCodeWindow that we just created.
            if (!_designerSettings.IsDesignerEnabled) {
                return codeWindow;
            }

            var AvaloniaDesignerPane = new AvaloniaDesignerPane(codeWindow, documentBuffer, filePath, _designerSettings);
            SiteObject(AvaloniaDesignerPane);
            return AvaloniaDesignerPane;
        }

        private void SiteObject(IObjectWithSite objectWithSite)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (objectWithSite == null) {
                return;
            }

            var oleServiceProvider = (IOleServiceProvider)GetService(typeof(IOleServiceProvider));
            objectWithSite.SetSite(oleServiceProvider);
        }

        private IVsCodeWindow CreateVsCodeWindow(IVsTextLines documentBuffer)
        {
            // Create a code window adapter.
            var codeWindow = VisualStudioServices.VsEditorAdaptersFactoryService.CreateVsCodeWindowAdapter(_oleServiceProvider);

            // Disable the splitter control on the editor as leaving it enabled causes a crash if the
            // user tries to use it here :(
            var codeWindowEx = (IVsCodeWindowEx)codeWindow;
            var initView = new INITVIEW[1];
            codeWindowEx.Initialize((uint)_codewindowbehaviorflags.CWB_DEFAULT, VSUSERCONTEXTATTRIBUTEUSAGE.VSUC_Usage_Filter, "", "", 0, initView);

            //Associate our IVsTextLines with our new code window.
            ErrorHandler.ThrowOnFailure(codeWindow.SetBuffer(documentBuffer));
            return codeWindow;
        }

        public int SetSite(IServiceProvider psp)
        {
            _oleServiceProvider = psp;
            _serviceProvider = new ServiceProvider(psp);
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
                rguidLogicalView == VSConstants.LOGVIEWID_Designer) {
                return VSConstants.S_OK;
            }

            return VSConstants.E_NOTIMPL;
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            lock (this) {
                if (_serviceProvider != null) {
                    _serviceProvider.Dispose();
                    _serviceProvider = null;
                }
            }
        }
    }
}
