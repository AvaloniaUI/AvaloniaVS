using System;
using System.Windows;
using System.Windows.Controls;
using AvaloniaVS.Services;

namespace AvaloniaVS.Views
{
    public partial class AvaloniaPreviewerView : UserControl
    {
        private PreviewerProcess _process;

        public AvaloniaPreviewerView()
        {
            InitializeComponent();
            ShowLoading();
        }

        public PreviewerProcess Process
        {
            get => _process;
            set
            {
                if (_process != null)
                {
                    _process.FrameReceived -= FrameReceived;
                    _process.Resized -= Resized;
                }

                _process = value;

                if (_process != null)
                {
                    _process.FrameReceived += FrameReceived;
                    _process.Resized += Resized;
                }

                ShowLoading();
            }
        }

        private void FrameReceived(object sender, FrameReceivedEventArgs e)
        {
            preview.Source = e.Frame;
            ShowPreviewer();
        }

        private void Resized(object sender, ResizedEventArgs e)
        {
            preview.Width = e.Size.Width;
            preview.Height = e.Size.Height;
        }

        private void ShowLoading()
        {
            loading.Visibility = Visibility.Visible;
            previewScroll.Visibility = Visibility.Collapsed;
        }

        private void ShowPreviewer()
        {
            loading.Visibility = Visibility.Collapsed;
            previewScroll.Visibility = Visibility.Visible;
        }
    }
}
