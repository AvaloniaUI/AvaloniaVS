using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Avalonia.Remote.Protocol.Input;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell;
using Serilog;
using AvMouseButton = Avalonia.Remote.Protocol.Input.MouseButton;
using WpfMouseButton = System.Windows.Input.MouseButton;

namespace AvaloniaVS.Views
{
    public partial class AvaloniaPreviewer : UserControl, IDisposable
    {
        private PreviewerProcess _process;
        private bool _centerPreviewer;
        private Size _lastBitmapSize;

        public AvaloniaPreviewer()
        {
            InitializeComponent();
            Update(null);

            Loaded += AvaloniaPreviewer_Loaded;

            previewScroller.ScrollChanged += PreviewScroller_ScrollChanged;
        }

        private void AvaloniaPreviewer_Loaded(object sender, RoutedEventArgs e)
        {
            // Debugging will cause Loaded/Unloaded events to fire, we only want to do this
            // the first time the designer is loaded, so unsub
            Loaded -= AvaloniaPreviewer_Loaded;            
            _centerPreviewer = true;
        }

        public PreviewerProcess Process
        {
            get => _process;
            set
            {
                if (_process != null)
                {
                    _process.ErrorChanged -= Update;
                    _process.FrameReceived -= Update;
                }

                _process = value;

                if (_process != null)
                {
                    _process.ErrorChanged += Update;
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

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) => Update(_process?.Bitmap);

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                previewScroller.ScrollToHorizontalOffset(
                       previewScroller.HorizontalOffset - (2 * e.Delta) / 120 * 48);

                e.Handled = true;
            }

            base.OnPreviewMouseWheel(e);
        }

        private double GetScaling()
        {
            var result = Process?.Scaling ?? 1;
            return result > 0 ? result : 1;
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
                var scaling = VisualTreeHelper.GetDpi(this).DpiScaleX;

                // If an error in the Xaml is present, we get a bitmap with width/height = 1
                // Which isn't ideal, but also messes up the scroll location since it will
                // trigger it to re-center, so only change the size
                // if the process shows we don't have an error
                if (Process.Error == null)
                {
                    preview.Width = bitmap.Width / scaling;
                    preview.Height = bitmap.Height / scaling;
                }
                
                loading.Visibility = Visibility.Collapsed;
                previewScroller.Visibility = Visibility.Visible;

                var fullScaling = scaling * Process.Scaling;
                var hScale = preview.Width * 2 / fullScaling;
                var vScale = preview.Height * 2 / fullScaling;
                previewGrid.Margin = new Thickness(hScale, vScale, hScale, vScale);

                // The bitmap size only changes if
                // 1- The design size changes
                // 2- The scaling changes from zoom factor
                // 3- The DPI changes
                // To ensure we don't have the ScrollViewer end up in a weird place,
                // recenter the content if the size changes
                if (preview.Width != _lastBitmapSize.Width ||
                    preview.Height != _lastBitmapSize.Height)
                {
                    _centerPreviewer = true;
                    _lastBitmapSize = new Size(preview.Width, preview.Height);
                }                
            }
            else
            {
                loading.Visibility = Visibility.Visible;
                previewScroller.Visibility = Visibility.Collapsed;
            }
        }

        private void PreviewScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_centerPreviewer)
            {
                // We can't do this in Update because the Scroll info may not be updated 
                // yet and the scrollable size may still be old
                previewScroller.ScrollToHorizontalOffset(previewScroller.ScrollableWidth / 2);
                previewScroller.ScrollToVerticalOffset(previewScroller.ScrollableHeight / 2);
                _centerPreviewer = false;
            }
        }

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(preview);
            var scaling = GetScaling();

            Process?.SendInputAsync(new PointerMovedEventMessage
            {
                X = p.X / scaling,
                Y = p.Y / scaling,
                Modifiers = GetModifiers(e),
            });
        }

        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var p = e.GetPosition(preview);
            var scaling = GetScaling();

            Process?.SendInputAsync(new PointerPressedEventMessage
            {
                X = p.X / scaling,
                Y = p.Y / scaling,
                Button = GetButton(e.ChangedButton),
                Modifiers = GetModifiers(e),
            });
        }

        private void Preview_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var p = e.GetPosition(preview);
            var scaling = GetScaling();

            Process?.SendInputAsync(new PointerReleasedEventMessage
            {
                X = p.X / scaling,
                Y = p.Y / scaling,
                Button = GetButton(e.ChangedButton),
                Modifiers = GetModifiers(e),
            });
        }

        private static AvMouseButton GetButton(WpfMouseButton button)
        {
            switch (button)
            {
                case WpfMouseButton.Left: return AvMouseButton.Left;
                case WpfMouseButton.Middle: return AvMouseButton.Middle;
                case WpfMouseButton.Right: return AvMouseButton.Right;
                default: return AvMouseButton.None;
            }
        }

        private static InputModifiers[] GetModifiers(MouseEventArgs e)
        {
            var result = new List<InputModifiers>();

            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            {
                result.Add(InputModifiers.Alt);
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                result.Add(InputModifiers.Control);
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                result.Add(InputModifiers.Shift);
            }

            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
            {
                result.Add(InputModifiers.Windows);
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                result.Add(InputModifiers.LeftMouseButton);
            }

            if (e.RightButton == MouseButtonState.Pressed)
            {
                result.Add(InputModifiers.RightMouseButton);
            }

            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                result.Add(InputModifiers.MiddleMouseButton);
            }

            return result.ToArray();
        }
    }
}
