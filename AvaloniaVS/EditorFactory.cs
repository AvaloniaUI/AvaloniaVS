using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace AvaloniaVS
{
    public sealed class EditorFactory : IVsEditorFactory, IDisposable
    {
        readonly AvaloniaPackage _package;
        ServiceProvider _serviceProvider;

        public EditorFactory(AvaloniaPackage package) => _package = package;

        public int Close() => VSConstants.S_OK;

        public int CreateEditorInstance(uint grfCreateDoc, string pszMkDocument, string pszPhysicalView, IVsHierarchy pvHier, uint itemid, IntPtr punkDocDataExisting, out IntPtr ppunkDocView, out IntPtr ppunkDocData, out string pbstrEditorCaption, out Guid pguidCmdUI, out int pgrfCDW)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }

        public int SetSite(IOleServiceProvider psp)
        {
            _serviceProvider = new ServiceProvider(psp);
            return VSConstants.S_OK;
        }

        public int MapLogicalView(ref Guid rguidLogicalView, out string pbstrPhysicalView)
        {
            throw new NotImplementedException();
        }
    }
}
