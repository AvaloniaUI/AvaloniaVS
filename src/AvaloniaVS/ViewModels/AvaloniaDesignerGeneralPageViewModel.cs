using System.ComponentModel.Composition;
using AvaloniaVS.Infrastructure;

namespace AvaloniaVS.ViewModels
{
    [Export, PartCreationPolicy(CreationPolicy.NonShared)]
    public class AvaloniaDesignerGeneralPageViewModel : ViewModelBase, IAvaloniaDesignerGeneralPageViewModel
    {
        private readonly IAvaloniaDesignerSettings _settings;
        private bool _isDesignerEnabled;
        private DocumentView _documentView;
        private SplitOrientation _splitOrientation;
        private bool _isReversed;

        [ImportingConstructor]
        public AvaloniaDesignerGeneralPageViewModel(IAvaloniaDesignerSettings settings)
        {
            _settings = settings;
            LoadSettings();
        }

        public bool IsDesignerEnabled
        {
            get { return _isDesignerEnabled; }
            set
            {
                if (_isDesignerEnabled == value)
                {
                    return;
                }
                _isDesignerEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsReversed
        {
            get { return _isReversed; }
            set
            {
                if (_isReversed == value)
                {
                    return;
                }
                _isReversed = value;
                RaisePropertyChanged();
            }
        }

        public DocumentView DocumentView
        {
            get { return _documentView; }
            set
            {
                if (_documentView == value)
                {
                    return;
                }
                _documentView = value;
                RaisePropertyChanged();
            }
        }

        public SplitOrientation SplitOrientation
        {
            get { return _splitOrientation; }
            set
            {
                if (_splitOrientation == value)
                {
                    return;
                }
                _splitOrientation = value;
                RaisePropertyChanged();
            }
        }

        private void LoadSettings()
        {
            IsDesignerEnabled = _settings.IsDesignerEnabled;
            DocumentView = _settings.DocumentView;
            SplitOrientation = _settings.SplitOrientation;
            IsReversed = _settings.IsReversed;
        }

        public void ApplyChanges()
        {
            _settings.IsDesignerEnabled = IsDesignerEnabled;
            _settings.DocumentView = DocumentView;
            _settings.SplitOrientation = SplitOrientation;
            _settings.IsReversed = IsReversed;
            _settings.Save();
        }
    }
}