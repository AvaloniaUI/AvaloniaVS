using System;
using System.Runtime.InteropServices;
using AvaloniaVS.Services;
using AvaloniaVS.Views;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor.Internal;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Serilog;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace AvaloniaVS.Shared.Views
{
    [ComVisible(true)]
    internal class EditorPane : WindowPane,
        IOleCommandTarget,
        IVsDeferredDocView,
        IVsFindTarget,
        IVsFindTarget2,
        IVsFindTarget3,
        IVsDropdownBarManager,
        IVsUserData,
        IVsHasRelatedSaveItems,
        IVsToolboxUser,
        IVsStatusbarUser,
        IVsCodeWindow,
        IVsCodeWindowEx,
        IConnectionPointContainer,
        IVsWindowFrameNotify2,
        IObjectWithSite,
        IServiceProvider,
        IVsBackForwardNavigation,
        IVsBackForwardNavigation2,
        IVsDocOutlineProvider,
        IVsTextEditorPropertyContainer
    {
        private const int WM_KEYFIRST = 0x0100;
        private const int WM_KEYLAST = 0x0109;

        private TextEditorHost _textEditorHost;
        private IVsFilterKeys2 _filterKeys;
        private AvaloniaDesigner _content;
        private bool _isInitialized;
        private bool _hasCreatedCodeWindow;
        private Project _project;
        private DTEEvents _dteEvents;
        private BuildEvents _buildEvents;
        private bool _isPaused;

        public EditorPane(Project project, TextEditorHost editorHost)
        {
            _project = project;
            _textEditorHost = editorHost;
            editorHost.CodeWindowCreated += EditorHostCodeWindowCreated;

            // We have to create this now. Shortly after VS calls Initialize(), it queries the Content property
            // which cannot return null or the designer will E_FAIL via 'Catastrophic COM error'. Note that the
            // IVsCodeWindow is not attached until the call to InitializeEditorPane() which won't happen until
            // the EditorPane is initialized and the underlying IVsTextBuffer is initialized at which point
            // the AvaloniaDesigner is fully initialized and the previewer process is started
            _content = new AvaloniaDesigner();
        }

        public override object Content => _content;

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_textEditorHost.VsCodeWindow is IOleCommandTarget oleCT)
                return oleCT.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_textEditorHost.VsCodeWindow is IOleCommandTarget oleCT)
                return oleCT.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        protected override bool PreProcessMessage(ref System.Windows.Forms.Message m)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (m.Msg >= WM_KEYFIRST && m.Msg <= WM_KEYLAST)
            {
                var oleMsg = new MSG
                {
                    hwnd = m.HWnd,
                    lParam = m.LParam,
                    wParam = m.WParam,
                    message = (uint)m.Msg
                };

                if (_filterKeys == null)
                {
                    _filterKeys = this.GetService<IVsFilterKeys2, SVsFilterKeys>();
                }

                return _filterKeys.TranslateAcceleratorEx(
                    new[] { oleMsg },
                    (uint)__VSTRANSACCELEXFLAGS.VSTAEXF_UseTextEditorKBScope,
                    0,
                    Array.Empty<Guid>(),
                    out var _,
                    out var _,
                    out var _,
                    out var _) == VSConstants.S_OK;
            }

            return base.PreProcessMessage(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            var tm = GetService(typeof(SVsTextManager)) as IVsTextManager;
            tm?.UnregisterIndependentView(this, _textEditorHost.TextBuffer);

            _content?.Dispose();
            _content = null;
        }

        protected override void Initialize()
        {
            base.Initialize();
            _isInitialized = true;

            // It's possible TextBuffer was initialized before we subscribed to the event, so we'll never 
            // get that notification and the designer won't initialize propertly
            // This happens if a file is closed and reopened in the same session when the text buffer is still
            // stored in the RunningDocumentTable and is just reloaded

            var isCodeWindowCreated = _textEditorHost.VsCodeWindow != null;

            if (isCodeWindowCreated && !_hasCreatedCodeWindow)
            {
                // Make sure we unsubscribe to this event now since EditorHostCodeWindowCreated won't be called
                _textEditorHost.CodeWindowCreated -= EditorHostCodeWindowCreated;
            }

            _hasCreatedCodeWindow = isCodeWindowCreated;

            // Only continue with initialization here if the IVsTextBuffer has finished initializing
            // and is ready - otherwise we won't be able to get the underlying ITextBuffer to set the
            // XamlBufferMetadata on and the designer will fail to load
            if (_hasCreatedCodeWindow)
            {
                InitializeEditorPane();
            }
        }

        private void EditorHostCodeWindowCreated(object sender, EventArgs e)
        {
            // We don't need to listen to buffer creation any more, release this event
            _textEditorHost.CodeWindowCreated -= EditorHostCodeWindowCreated;
            _hasCreatedCodeWindow = true;

            // If this calls before initialization, we'll error out in InitializeEditorPane as the call
            // to GetMefService<IAvaloniaVsSettings> will fail since the service provider hasn't been set
            if (!_isInitialized)
                return;

            // Initialize was called first, so we're good to go, let's do our initialization now
            InitializeEditorPane();
        }

        private void InitializeEditorPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Not 100% sure what this does, but I think its related to ensuring the IVsTextBuffer
            // is properly associated with its View
            var tm = (IVsTextManager)GetService(typeof(SVsTextManager));
            tm.RegisterIndependentView(this, _textEditorHost.TextBuffer);

            // Sub to events related to building so we can respond accordingly
            var dte = (DTE)Package.GetGlobalService(typeof(DTE));
            _buildEvents = dte.Events.BuildEvents;
            _dteEvents = dte.Events.DTEEvents;
            _isPaused = dte.Mode == vsIDEMode.vsIDEModeDebug;

            _buildEvents.OnBuildBegin += HandleBuildBegin;
            _buildEvents.OnBuildDone += HandleBuildDone;
            _dteEvents.ModeChanged += HandleModeChanged;

            var settings = this.GetMefService<IAvaloniaVSSettings>();
            var xamlEditorView = _content;
            xamlEditorView.IsPaused = _isPaused;
            xamlEditorView.SplitOrientation = settings.DesignerSplitOrientation;
            xamlEditorView.View = settings.DesignerView;
            xamlEditorView.ZoomLevel = settings.ZoomLevel;
            xamlEditorView.Start(_project, _textEditorHost.FileName, _textEditorHost.WpfTextViewHost);
        }

        private void HandleModeChanged(vsIDEMode lastMode)
        {
            if (_content != null)
            {
                _content.IsPaused = _isPaused = lastMode == vsIDEMode.vsIDEModeDesign;
            }
        }

        private void HandleBuildBegin(vsBuildScope Scope, vsBuildAction Action)
        {
            Log.Logger.Debug("Build started");

            _isPaused = true;

            if (_content != null)
            {
                _content.IsPaused = _isPaused;
            }
        }

        private void HandleBuildDone(vsBuildScope Scope, vsBuildAction Action)
        {
            Log.Logger.Debug("Build finished");

            _isPaused = false;

            if (_content != null)
            {
                _content.InvalidateCompletionMetadata();
                _content.IsPaused = _isPaused;
            }
        }

        int IVsDeferredDocView.get_CmdUIGuid(out System.Guid pGuidCmdId)
        {
            pGuidCmdId = Constants.AvaloviaFactoryEditorGuid;
            return VSConstants.S_OK;
        }

        int IVsDeferredDocView.get_DocView(out System.IntPtr ppUnkDocView)
        {
            ppUnkDocView = Marshal.GetIUnknownForObject(this);
            return VSConstants.S_OK;
        }

        // NOTE (amwx): We have to null check the VsCodeWindow. It is delayed in when its created
        // (see TextEditorHost for more) and some of these may be called in the in-between
        // time. Wasn't completely sure what to do then, so I'm returning E_PENDING. Some
        // of these require either S_OK or an error code (don't use S_FALSE), and E_PENDING
        // sounds reasonable since we're waiting on something else
        // As far as I can tell, there's no side effects of doing this everything still
        // initializes and works fine
        // UPDATE: After implementing IVsDeferredDocView, these are no longer called
        // earlier than we create the IVsCodeWindow, so it may not be necessary anymore
        // But for the time being, I'm leaving the edits just in case
#pragma warning disable VSTHRD010
        int IVsFindTarget.GetCapabilities(bool[] pfImage, uint[] pgrfOptions) =>
            (_textEditorHost.VsCodeWindow as IVsFindTarget)?.GetCapabilities(pfImage, pgrfOptions) ?? VSConstants.E_PENDING;
        int IVsFindTarget.GetProperty(uint propid, out object pvar)
        {
            if (_textEditorHost.VsCodeWindow is IVsFindTarget ft)
                return ft.GetProperty(propid, out pvar);

            pvar = null;
            return VSConstants.E_PENDING;
        }
        int IVsFindTarget.GetSearchImage(uint grfOptions, IVsTextSpanSet[] ppSpans, out IVsTextImage ppTextImage)
        {
            if (_textEditorHost.VsCodeWindow is IVsFindTarget ft)
                return ft.GetSearchImage(grfOptions, ppSpans, out ppTextImage);

            ppTextImage = null;
            return VSConstants.E_PENDING;
        }
        int IVsFindTarget.Find(string pszSearch, uint grfOptions, int fResetStartPoint, IVsFindHelper pHelper, out uint pResult)
        {
            if (_textEditorHost.VsCodeWindow is IVsFindTarget ft)
                return ft.Find(pszSearch, grfOptions, fResetStartPoint, pHelper, out pResult);

            pResult = 0;
            return VSConstants.E_PENDING;
        }
        int IVsFindTarget.Replace(string pszSearch, string pszReplace, uint grfOptions, int fResetStartPoint, IVsFindHelper pHelper, out int pfReplaced)
        {
            if (_textEditorHost.VsCodeWindow is IVsFindTarget ft)
                return ft.Replace(pszSearch, pszReplace, grfOptions, fResetStartPoint, pHelper, out pfReplaced);

            pfReplaced = 0;
            return VSConstants.E_PENDING;
        }
        int IVsFindTarget.GetMatchRect(RECT[] prc) =>
            (_textEditorHost.VsCodeWindow as IVsFindTarget)?.GetMatchRect(prc) ?? VSConstants.E_PENDING;
        int IVsFindTarget.NavigateTo(TextSpan[] pts) =>
            (_textEditorHost.VsCodeWindow as IVsFindTarget)?.NavigateTo(pts) ?? VSConstants.E_PENDING;
        int IVsFindTarget.GetCurrentSpan(TextSpan[] pts) =>
            (_textEditorHost.VsCodeWindow as IVsFindTarget)?.GetCurrentSpan(pts) ?? VSConstants.E_PENDING;
        int IVsFindTarget.SetFindState(object pUnk) =>
            (_textEditorHost.VsCodeWindow as IVsFindTarget)?.SetFindState(pUnk) ?? VSConstants.E_PENDING;
        int IVsFindTarget.GetFindState(out object ppunk)
        {
            if (_textEditorHost.VsCodeWindow is IVsFindTarget ft)
                return ft.GetFindState(out ppunk);

            ppunk = null;
            return VSConstants.E_PENDING;
        }
        int IVsFindTarget.NotifyFindTarget(uint notification) =>
            (_textEditorHost.VsCodeWindow as IVsFindTarget)?.NotifyFindTarget(notification) ?? VSConstants.E_PENDING;
        int IVsFindTarget.MarkSpan(TextSpan[] pts) =>
            (_textEditorHost.VsCodeWindow as IVsFindTarget)?.MarkSpan(pts) ?? VSConstants.E_PENDING;
        int IVsFindTarget2.NavigateTo2(IVsTextSpanSet pSpans, TextSelMode iSelMode) =>
            (_textEditorHost.VsCodeWindow as IVsFindTarget2)?.NavigateTo2(pSpans, iSelMode) ?? VSConstants.E_PENDING;
        int IVsFindTarget3.get_IsNewUISupported() => 0;
        int IVsFindTarget3.NotifyShowingNewUI() => 0;
        int IVsDropdownBarManager.AddDropdownBar(int cCombos, IVsDropdownBarClient pClient) =>
            (_textEditorHost.VsCodeWindow as IVsDropdownBarManager)?.AddDropdownBar(cCombos, pClient) ?? VSConstants.E_PENDING;
        int IVsDropdownBarManager.GetDropdownBar(out IVsDropdownBar ppDropdownBar)
        {
            if (_textEditorHost.VsCodeWindow is IVsDropdownBarManager dbm)
                return dbm.GetDropdownBar(out ppDropdownBar);

            ppDropdownBar = null;
            return VSConstants.E_PENDING;
        }
        int IVsDropdownBarManager.RemoveDropdownBar() =>
            (_textEditorHost.VsCodeWindow as IVsDropdownBarManager)?.RemoveDropdownBar() ?? VSConstants.E_PENDING;
        int IVsUserData.GetData(ref Guid riidKey, out object pvtData)
        {
            if (_textEditorHost.VsCodeWindow is IVsUserData ud)
                return ud.GetData(ref riidKey, out pvtData);

            pvtData = null;
            return VSConstants.E_PENDING;
        }
        int IVsUserData.SetData(ref Guid riidKey, object vtData) =>
            (_textEditorHost.VsCodeWindow as IVsUserData)?.SetData(ref riidKey, vtData) ?? VSConstants.E_PENDING;
        int IVsHasRelatedSaveItems.GetRelatedSaveTreeItems(VSSAVETREEITEM saveItem, uint celt, VSSAVETREEITEM[] rgSaveTreeItems, out uint pcActual)
        {
            if (_textEditorHost.VsCodeWindow is IVsHasRelatedSaveItems si)
                return si.GetRelatedSaveTreeItems(saveItem, celt, rgSaveTreeItems, out pcActual);

            pcActual = 0;
            return VSConstants.E_PENDING;
        }
        int IVsToolboxUser.IsSupported(IDataObject pDO) => (_textEditorHost.VsCodeWindow as IVsToolboxUser)?.IsSupported(pDO) ?? VSConstants.E_PENDING;
        int IVsToolboxUser.ItemPicked(IDataObject pDO) => (_textEditorHost.VsCodeWindow as IVsToolboxUser)?.ItemPicked(pDO) ?? VSConstants.E_PENDING;
        int IVsStatusbarUser.SetInfo() => (_textEditorHost.VsCodeWindow as IVsStatusbarUser)?.SetInfo() ?? VSConstants.E_PENDING;
        int IVsCodeWindow.SetBuffer(IVsTextLines pBuffer) => _textEditorHost.VsCodeWindow?.SetBuffer(pBuffer) ?? VSConstants.E_PENDING;
        int IVsCodeWindow.GetBuffer(out IVsTextLines ppBuffer)
        {
            if (_textEditorHost.VsCodeWindow is IVsCodeWindow cw)
                return cw.GetBuffer(out ppBuffer);

            ppBuffer = null;
            return VSConstants.E_PENDING;
        }
        int IVsCodeWindow.GetPrimaryView(out IVsTextView ppView)
        {
            if (_textEditorHost.VsCodeWindow is IVsCodeWindow cw)
                return cw.GetPrimaryView(out ppView);

            ppView = null;
            return VSConstants.E_PENDING;
        }
        int IVsCodeWindow.GetSecondaryView(out IVsTextView ppView)
        {
            if (_textEditorHost.VsCodeWindow is IVsCodeWindow cw)
                return cw.GetSecondaryView(out ppView);

            ppView = null;
            return VSConstants.E_PENDING;
        }
        int IVsCodeWindow.SetViewClassID(ref Guid clsidView) =>
            _textEditorHost.VsCodeWindow?.SetViewClassID(ref clsidView) ?? VSConstants.E_PENDING;
        int IVsCodeWindow.GetViewClassID(out Guid pclsidView)
        {
            if (_textEditorHost.VsCodeWindow is IVsCodeWindow cw)
                return cw.GetViewClassID(out pclsidView);

            pclsidView = Guid.Empty;
            return VSConstants.E_PENDING;
        }
        int IVsCodeWindow.SetBaseEditorCaption(string[] pszBaseEditorCaption) =>
            _textEditorHost.VsCodeWindow?.SetBaseEditorCaption(pszBaseEditorCaption) ?? VSConstants.E_PENDING;
        int IVsCodeWindow.GetEditorCaption(READONLYSTATUS dwReadOnly, out string pbstrEditorCaption)
        {
            if (_textEditorHost.VsCodeWindow is IVsCodeWindow cw)
                return cw.GetEditorCaption(dwReadOnly, out pbstrEditorCaption);

            pbstrEditorCaption = string.Empty;
            return VSConstants.E_PENDING;
        }

        int IVsCodeWindow.Close() => _textEditorHost.VsCodeWindow?.Close() ?? VSConstants.E_PENDING;
        int IVsCodeWindow.GetLastActiveView(out IVsTextView ppView)
        {
            if (_textEditorHost.VsCodeWindow is IVsCodeWindow cw)
                return cw.GetLastActiveView(out ppView);

            ppView = null;
            return VSConstants.E_PENDING;
        }
        int IVsCodeWindowEx.Initialize(uint grfCodeWindowBehaviorFlags, VSUSERCONTEXTATTRIBUTEUSAGE usageAuxUserContext,
            string szNameAuxUserContext, string szValueAuxUserContext, uint InitViewFlags, INITVIEW[] pInitView) =>
            (_textEditorHost.VsCodeWindow as IVsCodeWindowEx)?.Initialize(grfCodeWindowBehaviorFlags, usageAuxUserContext, szNameAuxUserContext, szValueAuxUserContext, InitViewFlags, pInitView) ?? VSConstants.E_PENDING;
        int IVsCodeWindowEx.IsReadOnly() => (_textEditorHost.VsCodeWindow as IVsCodeWindowEx)?.IsReadOnly() ?? VSConstants.E_PENDING;
        void IConnectionPointContainer.EnumConnectionPoints(out IEnumConnectionPoints ppEnum)
        {
            ppEnum = null;
            if (_textEditorHost.VsCodeWindow is IConnectionPointContainer cpc)
                cpc.EnumConnectionPoints(out ppEnum);
        }
        void IConnectionPointContainer.FindConnectionPoint(ref Guid riid, out IConnectionPoint ppCP)
        {
            ppCP = null;
            if (_textEditorHost.VsCodeWindow is IConnectionPointContainer cpc)
                cpc.FindConnectionPoint(ref riid, out ppCP);
        }
        int IVsWindowFrameNotify2.OnClose(ref uint pgrfSaveOptions) => VSConstants.S_OK;
        void IObjectWithSite.SetSite(object pUnkSite) => (_textEditorHost.VsCodeWindow as IObjectWithSite)?.SetSite(pUnkSite);
        void IObjectWithSite.GetSite(ref Guid riid, out IntPtr ppvSite)
        {
            ppvSite = IntPtr.Zero;
            if (_textEditorHost.VsCodeWindow is IObjectWithSite ows)
                ows.GetSite(ref riid, out ppvSite);
        }
        int IServiceProvider.QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
        {
            if (_textEditorHost.VsCodeWindow is IServiceProvider sp)
                return sp.QueryService(ref guidService, ref riid, out ppvObject);

            ppvObject = IntPtr.Zero;
            return VSConstants.E_PENDING;
        }
        int IVsBackForwardNavigation.NavigateTo(IVsWindowFrame pFrame, string bstrData, object punk) =>
            (_textEditorHost.VsCodeWindow as IVsBackForwardNavigation)?.NavigateTo(pFrame, bstrData, punk) ?? VSConstants.E_PENDING;
        int IVsBackForwardNavigation.IsEqual(IVsWindowFrame pFrame, string bstrData, object punk, out int fReplaceSelf)
        {
            if (_textEditorHost.VsCodeWindow is IVsBackForwardNavigation bfn)
                return bfn.IsEqual(pFrame, bstrData, punk, out fReplaceSelf);

            fReplaceSelf = 0;
            return VSConstants.E_PENDING;
        }
        bool IVsBackForwardNavigation2.RequestAddNavigationItem(IVsWindowFrame frame) =>
            (_textEditorHost.VsCodeWindow as IVsBackForwardNavigation2)?.RequestAddNavigationItem(frame) ?? false;
        int IVsDocOutlineProvider.GetOutlineCaption(VSOUTLINECAPTION nCaptionType, out string pbstrCaption)
        {
            if (_textEditorHost.VsCodeWindow is IVsDocOutlineProvider dop)
                return dop.GetOutlineCaption(nCaptionType, out pbstrCaption);

            pbstrCaption = string.Empty;
            return VSConstants.E_PENDING;
        }
        int IVsDocOutlineProvider.GetOutline(out IntPtr phwnd, out IOleCommandTarget ppCmdTarget)
        {
            if (_textEditorHost.VsCodeWindow is IVsDocOutlineProvider op)
                return op.GetOutline(out phwnd, out ppCmdTarget);

            phwnd = IntPtr.Zero;
            ppCmdTarget = null;
            return VSConstants.E_PENDING;
        }
        int IVsDocOutlineProvider.ReleaseOutline(IntPtr hwnd, IOleCommandTarget pCmdTarget) =>
            (_textEditorHost.VsCodeWindow as IVsDocOutlineProvider)?.ReleaseOutline(hwnd, pCmdTarget) ?? VSConstants.E_PENDING;
        int IVsDocOutlineProvider.OnOutlineStateChange(uint dwMask, uint dwState) =>
            (_textEditorHost.VsCodeWindow as IVsDocOutlineProvider)?.OnOutlineStateChange(dwMask, dwState) ?? VSConstants.E_PENDING;
        int IVsTextEditorPropertyContainer.GetProperty(VSEDITPROPID idProp, out object pvar)
        {
            if (_textEditorHost.VsCodeWindow is IVsTextEditorPropertyContainer epc)
                return epc.GetProperty(idProp, out pvar);

            pvar = null;
            return VSConstants.E_PENDING;
        }
        int IVsTextEditorPropertyContainer.SetProperty(VSEDITPROPID idProp, object var) =>
            (_textEditorHost.VsCodeWindow as IVsTextEditorPropertyContainer)?.SetProperty(idProp, var) ?? VSConstants.E_PENDING;
        int IVsTextEditorPropertyContainer.RemoveProperty(VSEDITPROPID idProp) =>
            (_textEditorHost.VsCodeWindow as IVsTextEditorPropertyContainer)?.RemoveProperty(idProp) ?? VSConstants.E_PENDING;

#pragma warning restore VSTHRD010
    }
}
