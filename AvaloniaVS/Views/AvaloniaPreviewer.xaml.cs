using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Avalonia.Remote.Protocol.Input;
using AvaloniaVS.Services;
using Microsoft.VisualStudio.Shell;
using Serilog;
using AvMouseButton = Avalonia.Remote.Protocol.Input.MouseButton;

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

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(preview);

            Process?.SendInputAsync(new PointerMovedEventMessage
            {
                X = p.X,
                Y = p.Y,
                Modifiers = GetModifiers(e),
            });
        }

        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var p = e.GetPosition(preview);

            Process?.SendInputAsync(new PointerPressedEventMessage
            {
                X = p.X,
                Y = p.Y,
                Button = GetButton(e),
                Modifiers = GetModifiers(e),
            });
        }

        private void Preview_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var p = e.GetPosition(preview);

            Process?.SendInputAsync(new PointerReleasedEventMessage
            {
                X = p.X,
                Y = p.Y,
                Button = GetButton(e),
                Modifiers = GetModifiers(e),
            });
        }

        private static AvMouseButton GetButton(MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                return AvMouseButton.Left;
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                return AvMouseButton.Right;
            }
            else if (e.MiddleButton == MouseButtonState.Pressed)
            {
                return AvMouseButton.Middle;
            }

            return AvMouseButton.None;
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
