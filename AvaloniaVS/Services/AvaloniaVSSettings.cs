using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using AvaloniaVS.Views;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Serilog;
using Serilog.Events;

namespace AvaloniaVS.Services
{
    [Export(typeof(IAvaloniaVSSettings))]
    public class AvaloniaVSSettings : IAvaloniaVSSettings, INotifyPropertyChanged
    {
        private const string SettingsKey = nameof(AvaloniaVSSettings);
        private readonly WritableSettingsStore _settings;
        private Orientation _designerSplitOrientation;
        private AvaloniaDesignerView _designerView = AvaloniaDesignerView.Split;
        private LogEventLevel _minimumLogVerbosity = LogEventLevel.Information;
        private bool _useTempDir = false;
        private string _tempDirPath = "dttmp";

        [ImportingConstructor]
        public AvaloniaVSSettings(SVsServiceProvider vsServiceProvider)
        {
            var shellSettingsManager = new ShellSettingsManager(vsServiceProvider);
            _settings = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
            Load();
        }

        public Orientation DesignerSplitOrientation
        {
            get => _designerSplitOrientation;
            set
            {
                if (_designerSplitOrientation != value)
                {
                    _designerSplitOrientation = value;
                    RaisePropertyChanged();
                }
            }
        }

        public AvaloniaDesignerView DesignerView
        {
            get => _designerView;
            set
            {
                if (_designerView != value)
                {
                    _designerView = value;
                    RaisePropertyChanged();
                }
            }
        }

        public LogEventLevel MinimumLogVerbosity
        {
            get => _minimumLogVerbosity;
            set
            {
                if (_minimumLogVerbosity != value)
                {
                    _minimumLogVerbosity = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool UseTempDir
        {
            get => _useTempDir;
            set
            {
                if (_useTempDir != value)
                {
                    _useTempDir = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string TempDirPath
        {
            get => _tempDirPath;
            set
            {
                if (_tempDirPath != value)
                {
                    _tempDirPath = value;
                    RaisePropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Load()
        {
            try
            {
                DesignerSplitOrientation = (Orientation)_settings.GetInt32(
                    SettingsKey,
                    nameof(DesignerSplitOrientation),
                    (int)Orientation.Vertical);
                DesignerView = (AvaloniaDesignerView)_settings.GetInt32(
                    SettingsKey,
                    nameof(DesignerView),
                    (int)AvaloniaDesignerView.Split);
                MinimumLogVerbosity = (LogEventLevel)_settings.GetInt32(
                    SettingsKey,
                    nameof(MinimumLogVerbosity),
                    (int)LogEventLevel.Information);
                UseTempDir = _settings.GetBoolean(
                    SettingsKey,
                    nameof(UseTempDir),
                    false);
                TempDirPath = _settings.GetString(
                    SettingsKey,
                    nameof(TempDirPath),
                    "dttmp");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load settings");
            }
        }

        public void Save()
        {
            try
            {
                if (!_settings.CollectionExists(SettingsKey))
                {
                    _settings.CreateCollection(SettingsKey);
                }

                _settings.SetInt32(SettingsKey, nameof(DesignerSplitOrientation), (int)DesignerSplitOrientation);
                _settings.SetInt32(SettingsKey, nameof(DesignerView), (int)DesignerView);
                _settings.SetInt32(SettingsKey, nameof(MinimumLogVerbosity), (int)MinimumLogVerbosity);
                _settings.SetBoolean(SettingsKey, nameof(UseTempDir), UseTempDir);
                _settings.SetString(SettingsKey, nameof(TempDirPath), TempDirPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save settings");
            }
        }

        private void RaisePropertyChanged([CallerMemberName]string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
