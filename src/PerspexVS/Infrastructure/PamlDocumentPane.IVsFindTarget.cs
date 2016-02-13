using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace PerspexVS.Infrastructure
{
    public partial class PamlDocumentPane : IVsFindTarget, IVsFindTarget2
    {
        public int GetCapabilities(bool[] pfImage, uint[] pgrfOptions)
        {
            return VsFindTarget.GetCapabilities(pfImage, pgrfOptions);
        }

        public int GetProperty(uint propid, out object pvar)
        {
            return VsFindTarget.GetProperty(propid, out pvar);
        }

        public int GetSearchImage(uint grfOptions, IVsTextSpanSet[] ppSpans, out IVsTextImage ppTextImage)
        {
            return VsFindTarget.GetSearchImage(grfOptions, ppSpans, out ppTextImage);
        }

        public int Find(string pszSearch, uint grfOptions, int fResetStartPoint, IVsFindHelper pHelper, out uint pResult)
        {
            return VsFindTarget.Find(pszSearch, grfOptions, fResetStartPoint, pHelper, out pResult);
        }

        public int Replace(string pszSearch, string pszReplace, uint grfOptions, int fResetStartPoint, IVsFindHelper pHelper, out int pfReplaced)
        {
            return VsFindTarget.Replace(pszSearch, pszReplace, grfOptions, fResetStartPoint, pHelper, out pfReplaced);
        }

        public int GetMatchRect(RECT[] prc)
        {
            return VsFindTarget.GetMatchRect(prc);
        }

        public int NavigateTo(TextSpan[] pts)
        {
            return VsFindTarget.NavigateTo(pts);
        }

        public int GetCurrentSpan(TextSpan[] pts)
        {
            return VsFindTarget.GetCurrentSpan(pts);
        }

        public int SetFindState(object pUnk)
        {
            return VsFindTarget.SetFindState(pUnk);
        }

        public int GetFindState(out object ppunk)
        {
            return VsFindTarget.GetFindState(out ppunk);
        }

        public int NotifyFindTarget(uint notification)
        {
            return VsFindTarget.NotifyFindTarget(notification);
        }

        public int MarkSpan(TextSpan[] pts)
        {
            return VsFindTarget.MarkSpan(pts);
        }

        public int NavigateTo2(IVsTextSpanSet pSpans, TextSelMode iSelMode)
        {
            return ((IVsFindTarget2)VsFindTarget).NavigateTo2(pSpans, iSelMode);
        }
    }
}