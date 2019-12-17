﻿using System.ComponentModel;
using System.Windows.Controls;
using AvaloniaVS.Views;
using Serilog.Events;

namespace AvaloniaVS.Services
{
    public interface IAvaloniaVSSettings : INotifyPropertyChanged
    {
        Orientation DesignerSplitOrientation { get; set; }
        AvaloniaDesignerView DesignerView { get; set; }
        LogEventLevel MinimumLogVerbosity { get; set; }
        bool UseTempDir { get; set; }
        string TempDirPath { get; set; }
        void Save();
        void Load();
    }
}
