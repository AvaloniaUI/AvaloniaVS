using System;
using System.Runtime.InteropServices;

// Do not change the namespace; otherwise, search box is broken in VS2022
namespace Microsoft.VisualStudio.Editor.Internal
{
    [ComImport]
    [TypeIdentifier]
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
