using System;
using System.Runtime.InteropServices;
using AvaloniaVS.Views;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace AvaloniaVS.Services
{
    public sealed class EditorFactory : IVsEditorFactory, IDisposable
    {
        const int CLSCTX_INPROC_SERVER = 1;
        readonly AvaloniaPackage _package;
        ServiceProvider _serviceProvider;

        public EditorFactory(AvaloniaPackage package) => _package = package;

        public int SetSite(IOleServiceProvider psp)
        {
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

            IVsTextLines textBuffer;

            if (punkDocDataExisting == IntPtr.Zero)
            {
                var localRegistry = _serviceProvider.GetService<ILocalRegistry, SLocalRegistry>();

                if (localRegistry != null)
                {
                    var iid = typeof(IVsTextLines).GUID;
                    var CLSID_VsTextBuffer = typeof(VsTextBufferClass).GUID;

                    localRegistry.CreateInstance(CLSID_VsTextBuffer, null, ref iid, CLSCTX_INPROC_SERVER, out var ptr);

                    try
                    {
                        textBuffer = Marshal.GetObjectForIUnknown(ptr) as IVsTextLines;
                    }
                    finally
                    {
                        Marshal.Release(ptr);
                    }

                    if (textBuffer is IObjectWithSite ows)
                    {
                        var oleServiceProvider = _serviceProvider.GetService<IOleServiceProvider>();
                        ows.SetSite(oleServiceProvider);
                    }
                }
                else
                {
                    throw new NotSupportedException("Could not access local registry.");
                }
            }
            else
            {
                textBuffer = Marshal.GetObjectForIUnknown(punkDocDataExisting) as IVsTextLines;

                if (textBuffer == null)
                {
                    return VSConstants.VS_E_INCOMPATIBLEDOCDATA;
                }
            }

            var pane = new DesignerPane(pszMkDocument);
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
    }
}
