using System;
using System.Windows.Media.Imaging;

namespace AvaloniaVS.Services
{
    public class FrameReceivedEventArgs : EventArgs
    {
        public FrameReceivedEventArgs(BitmapSource bitmap) => Bitmap = bitmap;
        public BitmapSource Bitmap { get; }
    }
}
