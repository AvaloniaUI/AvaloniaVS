using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AvaloniaVS.Services
{
    [Export(typeof(IAvaloniaVSSettings))]
    public class AvaloniaVSSettings : IAvaloniaVSSettings, INotifyPropertyChanged
    {
        private const string SettingsKey = nameof(AvaloniaVSSettings);

        private readonly WritableSettingsStore _settings;

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName]string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        private bool _enableDesigner = true;

        public bool EnablePreview
        {
            get => _enableDesigner;
            set
            {
                if (_enableDesigner != value)
                {
                    _enableDesigner = value;
                    RaisePropertyChanged();
                }
            }
        }

        private DesignerViewType _designerViewType = DesignerViewType.Split;

        public DesignerViewType DesignerViewType
        {
            get => _designerViewType;
            set
            {
                if (_designerViewType != value)
                {
                    _designerViewType = value;
                    RaisePropertyChanged();
                }
            }
        }

        private LogEventLevel _minimumLogVerbosity = LogEventLevel.Information;

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

        [ImportingConstructor]
        public AvaloniaVSSettings(SVsServiceProvider vsServiceProvider)
        {
            var shellSettingsManager = new ShellSettingsManager(vsServiceProvider);
            _settings = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            Load();
        }

        public void Load()
        {
            try
            {
                EnablePreview = _settings.GetBoolean(SettingsKey, nameof(EnablePreview), true);
                DesignerViewType = (DesignerViewType)_settings.GetInt32(SettingsKey, nameof(DesignerViewType), (int)DesignerViewType.Split);
                MinimumLogVerbosity = (LogEventLevel)_settings.GetInt32(SettingsKey, nameof(MinimumLogVerbosity), (int)LogEventLevel.Information);
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

                _settings.SetBoolean(SettingsKey, nameof(EnablePreview), EnablePreview);
                _settings.SetInt32(SettingsKey, nameof(DesignerViewType), (int)DesignerViewType);
                _settings.SetInt32(SettingsKey, nameof(MinimumLogVerbosity), (int)MinimumLogVerbosity);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save settings");
            }
        }
    }
}
