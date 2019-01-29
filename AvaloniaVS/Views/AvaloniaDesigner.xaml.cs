using System;
using System.Windows;
using System.Windows.Controls;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace AvaloniaVS.Views
{
    public partial class AvaloniaDesigner : UserControl, IDisposable
    {
        private readonly Throttle<string> _throttle;
        private IWpfTextViewHost _xmlEditor;

        public AvaloniaDesigner()
        {
            InitializeComponent();
            _throttle = new Throttle<string>(
                TimeSpan.FromMilliseconds(300),
                UpdateXaml);
        }

        public IWpfTextViewHost XmlEditor
        {
            get => _xmlEditor;
            set
            {
                if (_xmlEditor?.TextView.TextBuffer is ITextBuffer2 oldBuffer)
                {
                    oldBuffer.ChangedOnBackground -= TextChanged;
                }

                _xmlEditor = value;
                editorHost.Child = _xmlEditor?.HostControl;

                if (_xmlEditor?.TextView.TextBuffer is ITextBuffer2 newBuffer)
                {
                    newBuffer.ChangedOnBackground += TextChanged;
                }
            }
        }

        public PreviewerProcess Process
        {
            get => previewer.Process;
            set => previewer.Process = value;
        }

        public void Dispose()
        {
            _throttle.Dispose();
            XmlEditor = null;
            previewer.Dispose();
        }

        private void TextChanged(object sender, TextContentChangedEventArgs e)
        {
            _throttle.Queue(e.After.GetText());
        }

        private void UpdateXaml(string xaml)
        {
            Process?.UpdateXamlAsync(xaml).FireAndForget();
        }
    }
}
