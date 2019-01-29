using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using AvaloniaVS.Services;

namespace AvaloniaVS.Views
{
    public partial class AvaloniaPreviewer : UserControl, IDisposable
    {
        private PreviewerProcess _process;

        public AvaloniaPreviewer()
        {
            InitializeComponent();
            Update(null);
        }

        public PreviewerProcess Process
        {
            get => _process;
            set
            {
                if (_process != null)
                {
                    _process.FrameReceived -= FrameReceived;
                }

                _process = value;

                if (_process != null)
                {
                    _process.FrameReceived += FrameReceived;
                }

                Update(_process?.Bitmap);
            }
        }

        public void Dispose()
        {
            Process = null;
            Update(null);
        }

        private void FrameReceived(object sender, FrameReceivedEventArgs e) => Update(e.Bitmap);

        private void Update(BitmapSource bitmap)
        {
            preview.Source = bitmap;

            if (bitmap != null)
            {
                preview.Width = bitmap.Width;
                preview.Height = bitmap.Height;
                loading.Visibility = Visibility.Collapsed;
                previewScroll.Visibility = Visibility.Visible;
            }
            else
            {
                loading.Visibility = Visibility.Visible;
                previewScroll.Visibility = Visibility.Collapsed;
            }
        }
    }
}
