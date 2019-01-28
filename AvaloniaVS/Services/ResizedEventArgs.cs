using System;
using System.Windows;

namespace AvaloniaVS.Services
{
    public class ResizedEventArgs : EventArgs
    {
        public ResizedEventArgs(Size size) => Size = size;
        public Size Size { get; }
    }
}
