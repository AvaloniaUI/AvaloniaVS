using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TextManager.Interop;

namespace AvaloniaVS.Internals
{
    // TODO: those are some of the interface that should be implemented by the AvaloniaDesignerPane, to be fully compatible with the 
    // internal implementation of VsCodeWindowAdapter

    [Guid("47AB8522-6BB1-4C85-BA88-E4B4513F8BE1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IVsFindTarget4
    {
        int get_IsAutonomous();
        int get_IsIncrementalSearchSupported();
    }

    [Guid("A2F0D62B-D0DD-4C59-AAB8-79CD20785451")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IVsFindTarget3
    {
        int get_IsNewUISupported();
        int NotifyShowingNewUI();
    }

    [Guid("F657BA30-A2E2-4381-9AC8-F51C73962284")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IVsEmbeddedCodeWindowHost
    {
        void OnNewEmbeddedCodeWindow(IVsCodeWindow codeWindow, bool isReadOnly);
        void OnCloseEmbeddedCodeWindow(IVsCodeWindow codeWindow);
    }
}
