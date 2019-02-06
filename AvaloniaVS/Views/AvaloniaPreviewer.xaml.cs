using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell;
using Serilog;

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
                    _process.FrameReceived -= Update;
                }

                _process = value;

                if (_process != null)
                {
                    _process.FrameReceived += Update;
                }

                Update(_process?.Bitmap);
            }
        }

        public void Dispose()
        {
            Process = null;
            Update(null);
        }

        private async void Update(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                Update(_process.Bitmap);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error updating previewer");
            }
        }

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
