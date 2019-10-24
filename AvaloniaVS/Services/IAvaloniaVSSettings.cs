using System;
using System.ComponentModel;
using Microsoft.VisualStudio.ComponentModelHost;
using Serilog.Events;

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

        LogEventLevel MinimumLogVerbosity { get; set; }

        void Save();

        void Load();
    }
}
