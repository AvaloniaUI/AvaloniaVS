using System;
using System.Windows;
using System.Windows.Controls;
using AvaloniaVS.Services;

namespace AvaloniaVS.Views
{
    public partial class AvaloniaPreviewerView : UserControl
    {
        public static readonly DependencyProperty ProcessProperty =
            DependencyProperty.Register(
                nameof(Process),
                typeof(PreviewerProcess),
                typeof(AvaloniaPreviewerView),
                new PropertyMetadata(ProcessChanged));

        public AvaloniaPreviewerView()
        {
            InitializeComponent();
            ShowLoading();
        }

        public PreviewerProcess Process
        {
            get => (PreviewerProcess)GetValue(ProcessProperty);
            set => SetValue(ProcessProperty, value);
        }

        private void Subscribe(PreviewerProcess process)
        {
            process.FrameReceived += FrameReceived;
            process.Resized += Resized;
            ShowLoading();
        }

        private void Unsubscribe(PreviewerProcess process)
        {
            process.FrameReceived -= FrameReceived;
            process.Resized -= Resized;
            preview.Source = null;
            previewScroll.Visibility = Visibility.Collapsed;
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

        private static void ProcessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (AvaloniaPreviewerView)d;

            if (e.OldValue is PreviewerProcess oldProcess)
            {
                sender.Unsubscribe(oldProcess);
            }

            if (e.NewValue is PreviewerProcess newProcess)
            {
                sender.Subscribe(newProcess);
            }
        }
    }
}
