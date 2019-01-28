using System;
using System.Windows.Media.Imaging;

namespace AvaloniaVS.Services
{
    public class FrameReceivedEventArgs : EventArgs
    {
        public FrameReceivedEventArgs(BitmapSource frame) => Frame = frame;
        public BitmapSource Frame { get; }
    }
}
