using System;
using System.Runtime.InteropServices;
using AvaloniaVS.Services;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace AvaloniaVS.Views
{
    /// <summary>
    /// A <see cref="WindowPane"/> which hosts a VS editor control.
    /// </summary>
    /// <remarks>
    /// This class extends <see cref="WindowPane"/> and implements the (ludicrous amount of)
    /// required plumbing to forward functionality to the hosted editor.
    /// </remarks>
    public abstract class EditorHostPane : WindowPane,
        IOleCommandTarget,
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

        private readonly IVsCodeWindow _editorWindow;
        private readonly IOleCommandTarget _editorCommandTarget;
        private IVsFilterKeys2 _filterKeys;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorHostPane"/> class;
        /// </summary>
        /// <param name="editorWindow">The editor window to host.</param>
        public EditorHostPane(IVsCodeWindow editorWindow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _editorWindow = editorWindow ?? throw new ArgumentNullException(nameof(editorWindow));
            _editorCommandTarget = (IOleCommandTarget)editorWindow;
        }

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _editorCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _editorCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
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

#pragma warning disable VSTHRD010
        int IVsFindTarget.GetCapabilities(bool[] pfImage, uint[] pgrfOptions) => ((IVsFindTarget)_editorWindow).GetCapabilities(pfImage, pgrfOptions);
        int IVsFindTarget.GetProperty(uint propid, out object pvar) => ((IVsFindTarget)_editorWindow).GetProperty(propid, out pvar);
        int IVsFindTarget.GetSearchImage(uint grfOptions, IVsTextSpanSet[] ppSpans, out IVsTextImage ppTextImage) => ((IVsFindTarget)_editorWindow).GetSearchImage(grfOptions, ppSpans, out ppTextImage);
        int IVsFindTarget.Find(string pszSearch, uint grfOptions, int fResetStartPoint, IVsFindHelper pHelper, out uint pResult) => ((IVsFindTarget)_editorWindow).Find(pszSearch, grfOptions, fResetStartPoint, pHelper, out pResult);
        int IVsFindTarget.Replace(string pszSearch, string pszReplace, uint grfOptions, int fResetStartPoint, IVsFindHelper pHelper, out int pfReplaced) => ((IVsFindTarget)_editorWindow).Replace(pszSearch, pszReplace, grfOptions, fResetStartPoint, pHelper, out pfReplaced);
        int IVsFindTarget.GetMatchRect(RECT[] prc) => ((IVsFindTarget)_editorWindow).GetMatchRect(prc);
        int IVsFindTarget.NavigateTo(TextSpan[] pts) => ((IVsFindTarget)_editorWindow).NavigateTo(pts);
        int IVsFindTarget.GetCurrentSpan(TextSpan[] pts) => ((IVsFindTarget)_editorWindow).GetCurrentSpan(pts);
        int IVsFindTarget.SetFindState(object pUnk) => ((IVsFindTarget)_editorWindow).SetFindState(pUnk);
        int IVsFindTarget.GetFindState(out object ppunk) => ((IVsFindTarget)_editorWindow).GetFindState(out ppunk);
        int IVsFindTarget.NotifyFindTarget(uint notification) => ((IVsFindTarget)_editorWindow).NotifyFindTarget(notification);
        int IVsFindTarget.MarkSpan(TextSpan[] pts) => ((IVsFindTarget)_editorWindow).MarkSpan(pts);
        int IVsFindTarget2.NavigateTo2(IVsTextSpanSet pSpans, TextSelMode iSelMode) => ((IVsFindTarget2)_editorWindow).NavigateTo2(pSpans, iSelMode);
        int IVsFindTarget3.get_IsNewUISupported() => 0;
        int IVsFindTarget3.NotifyShowingNewUI() => 0;
        int IVsDropdownBarManager.AddDropdownBar(int cCombos, IVsDropdownBarClient pClient) => ((IVsDropdownBarManager)_editorWindow).AddDropdownBar(cCombos, pClient);
        int IVsDropdownBarManager.GetDropdownBar(out IVsDropdownBar ppDropdownBar) => ((IVsDropdownBarManager)_editorWindow).GetDropdownBar(out ppDropdownBar);
        int IVsDropdownBarManager.RemoveDropdownBar() => ((IVsDropdownBarManager)_editorWindow).RemoveDropdownBar();
        int IVsUserData.GetData(ref Guid riidKey, out object pvtData) => ((IVsUserData)_editorWindow).GetData(ref riidKey, out pvtData);
        int IVsUserData.SetData(ref Guid riidKey, object vtData) => ((IVsUserData)_editorWindow).SetData(ref riidKey, vtData);
        int IVsHasRelatedSaveItems.GetRelatedSaveTreeItems(VSSAVETREEITEM saveItem, uint celt, VSSAVETREEITEM[] rgSaveTreeItems, out uint pcActual) => ((IVsHasRelatedSaveItems)_editorWindow).GetRelatedSaveTreeItems(saveItem, celt, rgSaveTreeItems, out pcActual);
        int IVsToolboxUser.IsSupported(IDataObject pDO) => ((IVsToolboxUser)_editorWindow).IsSupported(pDO);
        int IVsToolboxUser.ItemPicked(IDataObject pDO) => ((IVsToolboxUser)_editorWindow).ItemPicked(pDO);
        int IVsStatusbarUser.SetInfo() => ((IVsStatusbarUser)_editorWindow).SetInfo();
        int IVsCodeWindow.SetBuffer(IVsTextLines pBuffer) => _editorWindow.SetBuffer(pBuffer);
        int IVsCodeWindow.GetBuffer(out IVsTextLines ppBuffer) => _editorWindow.GetBuffer(out ppBuffer);
        int IVsCodeWindow.GetPrimaryView(out IVsTextView ppView) => _editorWindow.GetPrimaryView(out ppView);
        int IVsCodeWindow.GetSecondaryView(out IVsTextView ppView) => _editorWindow.GetSecondaryView(out ppView);
        int IVsCodeWindow.SetViewClassID(ref Guid clsidView) => _editorWindow.SetViewClassID(ref clsidView);
        int IVsCodeWindow.GetViewClassID(out Guid pclsidView) => _editorWindow.GetViewClassID(out pclsidView);
        int IVsCodeWindow.SetBaseEditorCaption(string[] pszBaseEditorCaption) => _editorWindow.SetBaseEditorCaption(pszBaseEditorCaption);
        int IVsCodeWindow.GetEditorCaption(READONLYSTATUS dwReadOnly, out string pbstrEditorCaption) => _editorWindow.GetEditorCaption(dwReadOnly, out pbstrEditorCaption);
        int IVsCodeWindow.Close() => _editorWindow.Close();
        int IVsCodeWindow.GetLastActiveView(out IVsTextView ppView) => _editorWindow.GetLastActiveView(out ppView);
        int IVsCodeWindowEx.Initialize(uint grfCodeWindowBehaviorFlags, VSUSERCONTEXTATTRIBUTEUSAGE usageAuxUserContext, string szNameAuxUserContext, string szValueAuxUserContext, uint InitViewFlags, INITVIEW[] pInitView) => ((IVsCodeWindowEx)_editorWindow).Initialize(grfCodeWindowBehaviorFlags, usageAuxUserContext, szNameAuxUserContext, szValueAuxUserContext, InitViewFlags, pInitView);
        int IVsCodeWindowEx.IsReadOnly() => ((IVsCodeWindowEx)_editorWindow).IsReadOnly();
        void IConnectionPointContainer.EnumConnectionPoints(out IEnumConnectionPoints ppEnum) => ((IConnectionPointContainer)_editorWindow).EnumConnectionPoints(out ppEnum);
        void IConnectionPointContainer.FindConnectionPoint(ref Guid riid, out IConnectionPoint ppCP) => ((IConnectionPointContainer)_editorWindow).FindConnectionPoint(ref riid, out ppCP);
        int IVsWindowFrameNotify2.OnClose(ref uint pgrfSaveOptions) => VSConstants.S_OK;
        void IObjectWithSite.SetSite(object pUnkSite) => ((IObjectWithSite)_editorWindow).SetSite(pUnkSite);
        void IObjectWithSite.GetSite(ref Guid riid, out IntPtr ppvSite) => ((IObjectWithSite)_editorWindow).GetSite(ref riid, out ppvSite);
        int IServiceProvider.QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject) => ((IServiceProvider)_editorWindow).QueryService(ref guidService, ref riid, out ppvObject);
        int IVsBackForwardNavigation.NavigateTo(IVsWindowFrame pFrame, string bstrData, object punk) => ((IVsBackForwardNavigation)_editorWindow).NavigateTo(pFrame, bstrData, punk);
        int IVsBackForwardNavigation.IsEqual(IVsWindowFrame pFrame, string bstrData, object punk, out int fReplaceSelf) => ((IVsBackForwardNavigation)_editorWindow).IsEqual(pFrame, bstrData, punk, out fReplaceSelf);
        bool IVsBackForwardNavigation2.RequestAddNavigationItem(IVsWindowFrame frame) => ((IVsBackForwardNavigation2)_editorWindow).RequestAddNavigationItem(frame);
        int IVsDocOutlineProvider.GetOutlineCaption(VSOUTLINECAPTION nCaptionType, out string pbstrCaption) => ((IVsDocOutlineProvider)_editorWindow).GetOutlineCaption(nCaptionType, out pbstrCaption);
        int IVsDocOutlineProvider.GetOutline(out IntPtr phwnd, out IOleCommandTarget ppCmdTarget) => ((IVsDocOutlineProvider)_editorWindow).GetOutline(out phwnd, out ppCmdTarget);
        int IVsDocOutlineProvider.ReleaseOutline(IntPtr hwnd, IOleCommandTarget pCmdTarget) => ((IVsDocOutlineProvider)_editorWindow).ReleaseOutline(hwnd, pCmdTarget);
        int IVsDocOutlineProvider.OnOutlineStateChange(uint dwMask, uint dwState) => ((IVsDocOutlineProvider)_editorWindow).OnOutlineStateChange(dwMask, dwState);
        int IVsTextEditorPropertyContainer.GetProperty(VSEDITPROPID idProp, out object pvar) => ((IVsTextEditorPropertyContainer)_editorWindow).GetProperty(idProp, out pvar);
        int IVsTextEditorPropertyContainer.SetProperty(VSEDITPROPID idProp, object var) => ((IVsTextEditorPropertyContainer)_editorWindow).SetProperty(idProp, var);
        int IVsTextEditorPropertyContainer.RemoveProperty(VSEDITPROPID idProp) => ((IVsTextEditorPropertyContainer)_editorWindow).RemoveProperty(idProp);
#pragma warning restore VSTHRD010
    }
}
