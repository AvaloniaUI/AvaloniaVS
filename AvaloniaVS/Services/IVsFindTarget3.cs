using System;
using System.Runtime.InteropServices;

namespace AvaloniaVS.Services
{
    [ComImport]
    [Guid("A2F0D62B-D0DD-4C59-AAB8-79CD20785451")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IVsFindTarget3
    {
        [PreserveSig]
        int get_IsNewUISupported();

        [PreserveSig]
        int NotifyShowingNewUI();
    }
}
