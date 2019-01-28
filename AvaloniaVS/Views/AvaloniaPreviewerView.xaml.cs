using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using AvaloniaVS.Services;

namespace AvaloniaVS.Views
{
    public partial class AvaloniaPreviewerView : UserControl
    {
        private PreviewerProcess _process;

        public AvaloniaPreviewerView()
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

        private void FrameReceived(object sender, FrameReceivedEventArgs e) => Update(e.Bitmap);

        private void Update(BitmapSource bitmap)
        {
            if (bitmap != null)
            {
                preview.Source = bitmap;
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
