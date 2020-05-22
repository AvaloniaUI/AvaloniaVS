using System;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using AvaloniaVS.Models;
using AvaloniaVS.Views;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Serilog;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace AvaloniaVS.Services
{
    /// <summary>
    /// Implements <see cref="IVsEditorFactory"/> to create <see cref="DesignerPane"/>s containing
    /// an Avalonia XAML designer.
    /// </summary>
    internal sealed class EditorFactory : IVsEditorFactory, IDisposable
    {
        private static readonly Guid XmlLanguageServiceGuid = new Guid("f6819a78-a205-47b5-be1c-675b3c7f0b8e");
        private static readonly Guid XamlLanguageServiceGuid = new Guid("cd53c9a1-6bc2-412b-be36-cc715ed8dd41");
        private readonly AvaloniaPackage _package;
        private IOleServiceProvider _oleServiceProvider;
        private ServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorFactory"/> class.
        /// </summary>
        /// <param name="package">The package that the factory belongs to.</param>
        public EditorFactory(AvaloniaPackage package) => _package = package;

        /// <inheritdoc/>
        public int SetSite(IOleServiceProvider psp)
        {
            _oleServiceProvider = psp;
            _serviceProvider = new ServiceProvider(psp);
            return VSConstants.S_OK;
        }

        /// <inheritdoc/>
        public int MapLogicalView(ref Guid rguidLogicalView, out string pbstrPhysicalView)
        {
            pbstrPhysicalView = null;

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

        /// <inheritdoc/>
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

            Log.Logger.Verbose("Started EditorFactory.CreateEditorInstance({Filename})", pszMkDocument);

            ppunkDocView = IntPtr.Zero;
            ppunkDocData = IntPtr.Zero;
            pguidCmdUI = Guids.AvaloniaDesignerEditorFactory;
            pgrfCDW = 0;
            pbstrEditorCaption = string.Empty;

            if ((grfCreateDoc & (VSConstants.CEF_OPENFILE | VSConstants.CEF_SILENT)) == 0)
            {
                return VSConstants.E_INVALIDARG;
            }

            var project = GetProject(pvHier);

            if (project == null)
            {
                return VSConstants.S_FALSE;
            }

            var textBuffer = GetTextBuffer(pszMkDocument, punkDocDataExisting);
            var (editorWindow, editorControl) = CreateEditorControl(textBuffer);
            var pane = new DesignerPane(project, pszMkDocument, editorWindow, editorControl);
            ppunkDocView = Marshal.GetIUnknownForObject(pane);
            ppunkDocData = Marshal.GetIUnknownForObject(textBuffer);

            Log.Logger.Verbose("Finished EditorFactory.CreateEditorInstance({Filename})", pszMkDocument);
            return VSConstants.S_OK;
        }

        /// <inheritdoc/>
        public int Close() => VSConstants.S_OK;

        /// <inheritdoc/>
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

            Log.Logger.Verbose("Started EditorFactory.GetTextBuffer({Filename})", fileName);

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

            // Set buffer content type to XML. The default XAML content type will cause blue
            // squiggly lines to be displayed on the elements, as the XAML language service is
            // hard-coded as to the XAML dialects it supports and Avalonia isn't one of them :(
            ErrorHandler.ThrowOnFailure(result.SetLanguageServiceID(XmlLanguageServiceGuid));

            Log.Logger.Verbose("Finished EditorFactory.GetTextBuffer({Filename})", fileName);
            return result;
        }

        private (IVsCodeWindow, IWpfTextViewHost) CreateEditorControl(IVsTextLines bufferAdapter)
        {
            Log.Logger.Verbose("Started EditorFactory.CreateEditorControl()");

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

            // HACK: VS has given us an uninitialized IVsTextLines in punkDocDataExisting. Not sure what
            // we can do here except tell VS to close the tab and repopen it.
            if (buffer == null)
            {
                ErrorHandler.ThrowOnFailure(VSConstants.VS_E_INCOMPATIBLEDOCDATA);
            }

            buffer.Properties.GetOrCreateSingletonProperty(() => new XamlBufferMetadata());

            ErrorHandler.ThrowOnFailure(codeWindow.SetBuffer(bufferAdapter));
            ErrorHandler.ThrowOnFailure(codeWindow.GetPrimaryView(out var textViewAdapter));

            // In VS2019 preview 3, the IWpfTextViewHost.HostControl comes parented. Remove the
            // control from its parent otherwise we can't reparent it. This is probably a bug
            // in the preview and can probably be removed later.
            var textViewHost = eafs.GetWpfTextViewHost(textViewAdapter);

            if (textViewHost.HostControl.Parent is Decorator parent)
            {
                parent.Child = null;
            }

            Log.Logger.Verbose("Finished EditorFactory.CreateEditorControl()");
            return (codeWindow, textViewHost);
        }

        private static Project GetProject(IVsHierarchy hierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ErrorHandler.ThrowOnFailure(hierarchy.GetProperty(
                VSConstants.VSITEMID_ROOT,
                (int)__VSHPROPID.VSHPROPID_ExtObject,
                out var objProj));
            return objProj as Project;
        }
    }
}
