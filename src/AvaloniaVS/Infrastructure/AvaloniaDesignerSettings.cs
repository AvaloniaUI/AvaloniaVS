using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using AvaloniaVS.Helpers;

namespace AvaloniaVS.Infrastructure
{
    [Export(typeof(IAvaloniaDesignerSettings)), PartCreationPolicy(CreationPolicy.Shared)]
    public class AvaloniaDesignerSettings : IAvaloniaDesignerSettings
    {
        private const string COLLECTION_PATH = "AvaloniaDesigner";
        private const string DOCUMENT_VIEW_PROPERTY = "DocumentView";
        private const string SPLIT_ORIENTATION_PROPERTY = "SplitOrientation";
        private const string IS_DESIGNER_ENABLED_PROPERTY = "IsDesignerEnabled";
        private const string IS_REVERSED_PROPERTY = "IsReversed";

        private readonly WritableSettingsStore _store;

        [ImportingConstructor]
        public AvaloniaDesignerSettings(SVsServiceProvider vsServiceProvider)
        {
            _store = vsServiceProvider.GetWritableSettingsStore(SettingsScope.UserSettings);
            LoadSettings();
        }

        public bool IsDesignerEnabled { get; set; }
        public SplitOrientation SplitOrientation { get; set; }
        public DocumentView DocumentView { get; set; }
        public bool IsReversed { get; set; }

        private void LoadSettings()
        {
            // IsDesignerEnabled, either read from the store or initialize to true.
            IsDesignerEnabled = !_store.PropertyExists(COLLECTION_PATH, IS_DESIGNER_ENABLED_PROPERTY) || _store.GetBoolean(COLLECTION_PATH, IS_DESIGNER_ENABLED_PROPERTY);

            SplitOrientation = _store.PropertyExists(COLLECTION_PATH, SPLIT_ORIENTATION_PROPERTY)
                ? (SplitOrientation)_store.GetInt32(COLLECTION_PATH, SPLIT_ORIENTATION_PROPERTY)
                : SplitOrientation.Default;

            DocumentView = _store.PropertyExists(COLLECTION_PATH, DOCUMENT_VIEW_PROPERTY)
                ? (DocumentView)_store.GetInt32(COLLECTION_PATH, DOCUMENT_VIEW_PROPERTY)
                : DocumentView.SplitView;

            IsReversed = _store.PropertyExists(COLLECTION_PATH, IS_REVERSED_PROPERTY) && _store.GetBoolean(COLLECTION_PATH, IS_REVERSED_PROPERTY);
        }

        public void Save()
        {
            if (!_store.CollectionExists(COLLECTION_PATH))
            {
                _store.CreateCollection(COLLECTION_PATH);
            }

            _store.SetBoolean(COLLECTION_PATH, IS_DESIGNER_ENABLED_PROPERTY, IsDesignerEnabled);
            _store.SetBoolean(COLLECTION_PATH, IS_REVERSED_PROPERTY, IsReversed);
            _store.SetInt32(COLLECTION_PATH, SPLIT_ORIENTATION_PROPERTY, (int)SplitOrientation);
            _store.SetInt32(COLLECTION_PATH, DOCUMENT_VIEW_PROPERTY, (int)DocumentView);
        }
    }
}