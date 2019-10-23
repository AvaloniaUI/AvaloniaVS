using System;
using System.ComponentModel;
using Microsoft.VisualStudio.ComponentModelHost;

namespace AvaloniaVS.Services
{
    public enum DesignerViewType
    {
        Split,
        Design,
        Source
    }

    public interface IAvaloniaVSSettings : INotifyPropertyChanged
    {
        bool EnablePreview { get; set; }

        DesignerViewType DesignerViewType { get; set; }

        void Save();

        void Load();
    }
}
