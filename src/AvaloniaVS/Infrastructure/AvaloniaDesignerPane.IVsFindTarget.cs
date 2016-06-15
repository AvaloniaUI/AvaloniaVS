using System;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace AvaloniaVS.Infrastructure
{
    public partial class AvaloniaDesignerPane : IVsFindTarget,
        IVsFindTarget2,
        IVsDropdownBarManager,
        IVsUserData,
        IVsHasRelatedSaveItems,
        IVsToolboxUser,
        IVsStatusbarUser,
        IVsCodeWindow,
        IVsCodeWindowEx,
        IVsCodeWindow2,
        IConnectionPointContainer,
        IVsWindowFrameNotify2,
        IExtensibleObject,
        IObjectWithSite,
        Microsoft.VisualStudio.OLE.Interop.IServiceProvider,
        IVsBackForwardNavigation,
        IVsBackForwardNavigation2,
        IVsDocOutlineProvider,
        IVsTextEditorPropertyContainer
    {
        public int GetCapabilities(bool[] pfImage, uint[] pgrfOptions)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.GetCapabilities(pfImage, pgrfOptions);
        }

        public int GetProperty(uint propid, out object pvar)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.GetProperty(propid, out pvar);
        }

        public int GetSearchImage(uint grfOptions, IVsTextSpanSet[] ppSpans, out IVsTextImage ppTextImage)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.GetSearchImage(grfOptions, ppSpans, out ppTextImage);
        }

        public int Find(string pszSearch, uint grfOptions, int fResetStartPoint, IVsFindHelper pHelper, out uint pResult)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.Find(pszSearch, grfOptions, fResetStartPoint, pHelper, out pResult);
        }

        public int Replace(string pszSearch, string pszReplace, uint grfOptions, int fResetStartPoint, IVsFindHelper pHelper, out int pfReplaced)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.Replace(pszSearch, pszReplace, grfOptions, fResetStartPoint, pHelper, out pfReplaced);
        }

        public int GetMatchRect(RECT[] prc)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.GetMatchRect(prc);
        }

        public int NavigateTo(TextSpan[] pts)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.NavigateTo(pts);
        }

        public int GetCurrentSpan(TextSpan[] pts)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.GetCurrentSpan(pts);
        }

        public int SetFindState(object pUnk)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.SetFindState(pUnk);
        }

        public int GetFindState(out object ppunk)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.GetFindState(out ppunk);
        }

        public int NotifyFindTarget(uint notification)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.NotifyFindTarget(notification);
        }

        public int MarkSpan(TextSpan[] pts)
        {
            var findTarget = (IVsFindTarget)_vsCodeWindow;
            return findTarget.MarkSpan(pts);
        }

        public int NavigateTo2(IVsTextSpanSet pSpans, TextSelMode iSelMode)
        {
            var findTarget = (IVsFindTarget2)_vsCodeWindow;
            return findTarget.NavigateTo2(pSpans, iSelMode);
        }

        public IVsDropdownBarManager DropdownBarManager
        {
            get { return (IVsDropdownBarManager) _vsCodeWindow; }
        }

        public int AddDropdownBar(int cCombos, IVsDropdownBarClient pClient)
        {
            return DropdownBarManager.AddDropdownBar(cCombos, pClient);
        }

        public int GetDropdownBar(out IVsDropdownBar ppDropdownBar)
        {
            return DropdownBarManager.GetDropdownBar(out ppDropdownBar);
        }

        public int RemoveDropdownBar()
        {
            return DropdownBarManager.RemoveDropdownBar();
        }

        public IVsUserData VsUserData
        {
            get { return (IVsUserData) _vsCodeWindow; }
        }

        public int GetData(ref Guid riidKey, out object pvtData)
        {
            return VsUserData.GetData(ref riidKey, out pvtData);
        }

        public int SetData(ref Guid riidKey, object vtData)
        {
            return VsUserData.GetData(ref riidKey, out vtData);
        }

        public int GetRelatedSaveTreeItems(VSSAVETREEITEM saveItem, uint celt, VSSAVETREEITEM[] rgSaveTreeItems, out uint pcActual)
        {
            var codeWindow = (IVsHasRelatedSaveItems) _vsCodeWindow;
            return codeWindow.GetRelatedSaveTreeItems(saveItem, celt, rgSaveTreeItems, out pcActual);
        }

        public int IsSupported(IDataObject pDO)
        {
            var toolboxUser = (IVsToolboxUser) _vsCodeWindow;
            return toolboxUser.IsSupported(pDO);
        }

        public int ItemPicked(IDataObject pDO)
        {
            var toolboxUser = (IVsToolboxUser)_vsCodeWindow;
            return toolboxUser.IsSupported(pDO);
        }

        public int SetInfo()
        {
            var codeWindow = (IVsStatusbarUser) _vsCodeWindow;
            return codeWindow.SetInfo();
        }

        public int SetBuffer(IVsTextLines pBuffer)
        {
            return _vsCodeWindow.SetBuffer(pBuffer);
        }

        public int GetBuffer(out IVsTextLines ppBuffer)
        {
            return _vsCodeWindow.GetBuffer(out ppBuffer);
        }

        public int GetPrimaryView(out IVsTextView ppView)
        {
            return _vsCodeWindow.GetPrimaryView(out ppView);
        }

        public int GetSecondaryView(out IVsTextView ppView)
        {
            return _vsCodeWindow.GetSecondaryView(out ppView);
        }

        public int SetViewClassID(ref Guid clsidView)
        {
            return _vsCodeWindow.SetViewClassID(ref clsidView);
        }

        public int GetViewClassID(out Guid pclsidView)
        {
            return _vsCodeWindow.GetViewClassID(out pclsidView);
        }

        public int SetBaseEditorCaption(string[] pszBaseEditorCaption)
        {
            return _vsCodeWindow.SetBaseEditorCaption(pszBaseEditorCaption);
        }

        public int GetEditorCaption(READONLYSTATUS dwReadOnly, out string pbstrEditorCaption)
        {
            return _vsCodeWindow.GetEditorCaption(dwReadOnly, out pbstrEditorCaption);
        }

        public int Close()
        {
            return _vsCodeWindow.Close();
        }

        public int GetLastActiveView(out IVsTextView ppView)
        {
            return _vsCodeWindow.GetLastActiveView(out ppView);
        }

        public int Initialize(uint grfCodeWindowBehaviorFlags,
                              VSUSERCONTEXTATTRIBUTEUSAGE usageAuxUserContext,
                              string szNameAuxUserContext,
                              string szValueAuxUserContext,
                              uint initViewFlags,
                              INITVIEW[] pInitView)
        {
            var vsCodeWindow = (IVsCodeWindowEx) _vsCodeWindow;
            return vsCodeWindow.Initialize(grfCodeWindowBehaviorFlags, usageAuxUserContext, szNameAuxUserContext, szValueAuxUserContext, initViewFlags, pInitView);
        }

        public int IsReadOnly()
        {
            var vsCodeWindow = (IVsCodeWindowEx) _vsCodeWindow;
            return vsCodeWindow.IsReadOnly();
        }

        public int GetProperty(VSEDITPROPID idProp, out object pvar)
        {
            var codeWindow = (IVsTextEditorPropertyContainer) _vsCodeWindow;
            return codeWindow.GetProperty(idProp, out pvar);
        }

        public int SetProperty(VSEDITPROPID idProp, object var)
        {
            var codeWindow = (IVsTextEditorPropertyContainer)_vsCodeWindow;
            return codeWindow.SetProperty(idProp, var);
        }

        public int RemoveProperty(VSEDITPROPID idProp)
        {
            var codeWindow = (IVsTextEditorPropertyContainer)_vsCodeWindow;
            return codeWindow.RemoveProperty(idProp);
        }

        public void EnumConnectionPoints(out IEnumConnectionPoints ppEnum)
        {
            var codeWindow = (IConnectionPointContainer) _vsCodeWindow;
            codeWindow.EnumConnectionPoints(out ppEnum);
        }

        public void FindConnectionPoint(ref Guid riid, out IConnectionPoint ppCP)
        {
            var codeWindow = (IConnectionPointContainer) _vsCodeWindow;
            codeWindow.FindConnectionPoint(ref riid, out ppCP);
        }

        private IVsTextManager _textManager;
        internal IVsTextManager TextManager
        {
            get
            {
                if (this._textManager == null)
                    this._textManager = (IVsTextManager)this.GetService(typeof(SVsTextManager));
                return this._textManager;
            }
        }

        public int OnClose(ref uint pgrfSaveOptions)
        {
            // currently if we route the call to the _vsCodeWindow instance
            // we will get an 'System.NullReferenceException' in Microsoft.VisualStudio.Editor.Implementation.dll
            // for now we skip the call and return an OK

            return VSConstants.S_OK;

            //var codeWindow = (IVsWindowFrameNotify2)_vsCodeWindow;
            //return codeWindow.OnClose(ref pgrfSaveOptions);
        }

        public void GetAutomationObject(string Name, IExtensibleObjectSite pParent, out object ppDisp)
        {
            var extensibleObject = (IExtensibleObject) _vsCodeWindow;
            extensibleObject.GetAutomationObject(Name, pParent, out ppDisp);
        }

        public void SetSite(object pUnkSite)
        {
            var objectWithSite = (IObjectWithSite) _vsCodeWindow;
            objectWithSite.SetSite(pUnkSite);
        }

        public void GetSite(ref Guid riid, out IntPtr ppvSite)
        {
            var objectWithSite = (IObjectWithSite)_vsCodeWindow;
            objectWithSite.GetSite(ref riid, out ppvSite);
        }

        public int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
        {
            var codeWindow = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider) _vsCodeWindow;
            return codeWindow.QueryService(ref guidService, ref riid, out ppvObject);
        }

        public int NavigateTo(IVsWindowFrame pFrame, string bstrData, object punk)
        {
            var codeWindow = (IVsBackForwardNavigation) _vsCodeWindow;
            return codeWindow.NavigateTo(pFrame, bstrData, punk);
        }

        public int IsEqual(IVsWindowFrame pFrame, string bstrData, object punk, out int fReplaceSelf)
        {
            var codeWindow = (IVsBackForwardNavigation) _vsCodeWindow;
            return codeWindow.IsEqual(pFrame, bstrData, punk, out fReplaceSelf);
        }

        public bool RequestAddNavigationItem(IVsWindowFrame frame)
        {
            var codeWindow = (IVsBackForwardNavigation2)_vsCodeWindow;
            return codeWindow.RequestAddNavigationItem(frame);
        }

        public int GetOutlineCaption(VSOUTLINECAPTION nCaptionType, out string pbstrCaption)
        {
            var outlineProvider = (IVsDocOutlineProvider) _vsCodeWindow;
            return outlineProvider.GetOutlineCaption(nCaptionType, out pbstrCaption);
        }

        public int GetOutline(out IntPtr phwnd, out IOleCommandTarget ppCmdTarget)
        {
            var outlineProvider = (IVsDocOutlineProvider)_vsCodeWindow;
            return outlineProvider.GetOutline(out phwnd, out ppCmdTarget);
        }

        public int ReleaseOutline(IntPtr hwnd, IOleCommandTarget pCmdTarget)
        {
            var outlineProvider = (IVsDocOutlineProvider)_vsCodeWindow;
            return outlineProvider.ReleaseOutline(hwnd, pCmdTarget);
        }

        public int OnOutlineStateChange(uint dwMask, uint dwState)
        {
            var outlineProvider = (IVsDocOutlineProvider)_vsCodeWindow;
            return outlineProvider.OnOutlineStateChange(dwMask, dwState);
        }

        public int GetEmbeddedCodeWindowCount(out int piCount)
        {
            var vsCodeWindow = (IVsCodeWindow2) _vsCodeWindow;
            return vsCodeWindow.GetEmbeddedCodeWindowCount(out piCount);
        }

        public int GetEmbeddedCodeWindow(int iIndex, out IVsCodeWindow ppCodeWindow)
        {
            var vsCodeWindow = (IVsCodeWindow2)_vsCodeWindow;
            return vsCodeWindow.GetEmbeddedCodeWindow(iIndex, out ppCodeWindow);
        }

        public int GetContainingCodeWindow(out IVsCodeWindow ppCodeWindow)
        {
            var vsCodeWindow = (IVsCodeWindow2)_vsCodeWindow;
            return vsCodeWindow.GetContainingCodeWindow(out ppCodeWindow);
        }
    }
}