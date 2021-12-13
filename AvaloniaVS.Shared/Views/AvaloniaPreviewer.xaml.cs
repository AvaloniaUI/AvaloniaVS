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
                preview.Width = bitmap.Width / scaling;
                preview.Height = bitmap.Height / scaling;
                loading.Visibility = Visibility.Collapsed;
                previewScroll.Visibility = Visibility.Visible;
            }
            else
            {
                loading.Visibility = Visibility.Visible;
                previewScroll.Visibility = Visibility.Collapsed;
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
