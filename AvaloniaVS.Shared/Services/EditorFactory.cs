using System;
using System.Runtime.InteropServices;
using AvaloniaVS.Shared.Views;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
        private readonly AvaloniaPackage _package;
        private IOleServiceProvider _oleServiceProvider;
        private ServiceProvider _serviceProvider;

        private const string csExt = ".cs";
        private const string fsExt = ".fs";
        private const string vbExt = ".vb";
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
                rguidLogicalView == VSConstants.LOGVIEWID_Debugging ||
                rguidLogicalView == VSConstants.LOGVIEWID_TextView ||
                rguidLogicalView == VSConstants.LOGVIEWID_Designer)
            {
                return VSConstants.S_OK;
            }

            if(rguidLogicalView == VSConstants.LOGVIEWID_Code)
            {
                pbstrPhysicalView = "Code";
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
            pguidCmdUI = Constants.AvaloviaFactoryEditorGuid;
            pgrfCDW = 0;
            pbstrEditorCaption = string.Empty;

            if (pszPhysicalView == "Code")
            {

                // We only want to handle "View Code" if our designer pane is active
                if (!pszMkDocument.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) &&
                    !pszMkDocument.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                    return VSConstants.E_INVALIDARG;
                else
                {
                    if (GetExtensionObject(pvHier,itemid) is ProjectItem pi)
                    {
                        var codeFile = FindCodeFileForXaml(pi, out var codeProjectItem);
                        if (codeFile)
                        {
                            var wnd = codeProjectItem.Open(EnvDTE.Constants.vsViewKindTextView);
                            wnd.Activate();
                            return VSConstants.S_OK;
                        }
                    }
                }
                pszPhysicalView = null;
            }

            if ((grfCreateDoc & (VSConstants.CEF_OPENFILE | VSConstants.CEF_SILENT)) == 0)
            {
                return VSConstants.E_INVALIDARG;
            }

            // For reference of the new way this works:
            // https://github.com/madskristensen/EditorConfigLanguage/blob/master/src/LanguageService/EditorFactory.cs
            // and this sample
            // https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/WPFDesigner_XML/WPFDesigner_XML

            IVsTextLines textLines = GetTextBuffer(punkDocDataExisting);
            if (punkDocDataExisting != IntPtr.Zero)
            {
                // We had an existing text buffer
                ppunkDocData = punkDocDataExisting;
                Marshal.AddRef(ppunkDocData);
            }
            else
            {
                // We created our own VsTextBuffer
                ppunkDocData = Marshal.GetIUnknownForObject(textLines);
            }

            try
            {
                // Prepare the way for the IVsCodeWindow...Note that it may not be created yet
                // as we need to wait for the IVsTextBuffer to fully initialize first. This is all
                // handled from CreateDocumentView and the TextEditorHost
                var docViewObject = CreateDocumentView(pszMkDocument, pszPhysicalView, textLines,
                    punkDocDataExisting == IntPtr.Zero);

                // Create the pane that will host our previewer and will be associated with this text data
                var pane = new EditorPane(GetProject(pvHier), docViewObject);

                ppunkDocView = Marshal.GetIUnknownForObject(pane);
            }
            finally
            {
                if (ppunkDocView == IntPtr.Zero)
                {
                    if (punkDocDataExisting != ppunkDocData && ppunkDocData != IntPtr.Zero)
                    {
                        Marshal.Release(ppunkDocData);
                        ppunkDocData = IntPtr.Zero;
                    }
                }
            }

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

        private IVsTextLines GetTextBuffer(IntPtr docDataExisting)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsTextLines textLines;
            if (docDataExisting == IntPtr.Zero)
            {
                // Create a new IVsTextLines buffer.
                Log.Logger.Verbose("Creating new IVsTextBuffer");

                // amwx - The old way involved using an invisible editor, except that was failing
                // when calling GetDocData, most likely because the text buffer hadn't initialized
                // and there's no way to get the text buffer to query for initialization from
                // and invisible editor. But, there is a way to create it manually, and this seems
                // to be the "correct" way to do it now, and leads me to believe the invisible
                // editor is archaic and phased out internally and without announcement

                Type textLinesType = typeof(IVsTextLines);
                Guid riid = textLinesType.GUID;
                Guid clsid = typeof(VsTextBufferClass).GUID;
                textLines = _package.CreateInstance(ref clsid, ref riid, textLinesType) as IVsTextLines;

                // set the buffer's site
                ((IObjectWithSite)textLines).SetSite(_serviceProvider.GetService(typeof(IOleServiceProvider)));
            }
            else
            {
                Log.Logger.Verbose("Using Existing IVsTextBuffer");
                // Use the existing text buffer
                object dataObject = Marshal.GetObjectForIUnknown(docDataExisting);
                textLines = dataObject as IVsTextLines;
                if (textLines == null)
                {
                    // Try get the text buffer from textbuffer provider
                    if (dataObject is IVsTextBufferProvider textBufferProvider)
                    {
                        textBufferProvider.GetTextBuffer(out textLines);
                    }
                }
                if (textLines == null)
                {
                    // Unknown docData type then, so we have to force VS to close the other editor.
                    throw Marshal.GetExceptionForHR(VSConstants.VS_E_INCOMPATIBLEDOCDATA);
                }

            }
            return textLines;
        }

        private TextEditorHost CreateDocumentView(string documentMoniker, string physicalView, IVsTextLines textLines, bool createdDocData)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Log.Logger.Verbose("Creating Document View");
            if (string.IsNullOrEmpty(physicalView))
            {
                // create code window as default physical view
                var componentModel = _serviceProvider.GetService<IComponentModel, SComponentModel>();
                var editorHost = new TextEditorHost(textLines, documentMoniker, componentModel, _oleServiceProvider);

                if (!createdDocData)
                {
                    var adapterService = componentModel.GetService<IVsEditorAdaptersFactoryService>();
                    var buf = adapterService.GetDocumentBuffer(textLines);

                    // It seems we may get an uninitialized IVsTextBuffer here. Inspecting via break point shows the content type
                    // is "Inert" and the document/data buffers are empty, thus we aren't actually initialized yet? IIRC this
                    // relates to some change with intellisense around VS 2019 16.3-ish. So if we aren't initialized yet, we
                    // don't want to "force" OnLoadCompleted here and we'll wait for the event to actually fire
                    // We will only get an initialized text buffer if the document had been previously opened in the current
                    // session, closed, and now reopened and its buffer was in the RunningDocumentTable. 
                    if (buf != null)
                        editorHost.OnLoadCompleted(0);
                }

                return editorHost;
            }

            // We couldn't create the view
            // Return special error code so VS can try another editor factory.
            throw Marshal.GetExceptionForHR(VSConstants.VS_E_UNSUPPORTEDFORMAT);
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

        private static EnvDTE.ProjectItem GetExtensionObject(IVsHierarchy hierarchy, uint itemId)
        {
            object project;

            ThreadHelper.ThrowIfNotOnUIThread();

            ErrorHandler.ThrowOnFailure(
                hierarchy.GetProperty(
                    itemId,
                    (int)__VSHPROPID.VSHPROPID_ExtObject,
                    out project
                )
            );

            return (project as EnvDTE.ProjectItem);
        }

#pragma warning disable VSTHRD010 // Only called from ExecuteCommand, which does the thread check
        private bool FindCodeFileForXaml(ProjectItem projectItem, out ProjectItem codeProjectItem)
        {
            codeProjectItem = null;
            if (projectItem.ProjectItems.Count == 0)
                return false;

            // Search the project items under the current project item
            // "MainWindow.axaml" <-- projectItem
            //   "MainWindow.axaml.cs" <-- projectItem.ProjectItems
            foreach (ProjectItem pi in projectItem.ProjectItems)
            {
                if (IsCodeFile(pi.Name))
                {
                    codeProjectItem = pi;
                    return true;
                }
            }

            // TODO: If we reach here, it means VS isn't nesting the files like its suppposed
            // to:
            // Project
            //   MainWindow.axaml
            //   MainWindow.axaml.cs
            // We need to find the parent item, and search for it as a sibling
            // The parent item can be obtained through 'projectItem.Collection.Parent'
            // which will probably return either a ProjectItem or Project
            // and then run a search to see where it matches the name and ends in a lang extension
            // The issue here is how does this handle files with partial classes? Since I don't
            // have a way to test this (since VS seems to nest normally for me), marking as
            // a TODO and we'll return false to not handle it

            Log.Logger.Verbose("Attempted to view code for {Document}, but was unable to find nested code file", projectItem.Name);

            return false;
        }
#pragma warning restore

        private bool IsCodeFile(string name)
        {
            if (name.EndsWith(csExt, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(fsExt, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(vbExt, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
