using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Avalonia.Remote.Protocol.Input;
using AvaloniaVS.Services;
using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
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
        private WeakReference<BitmapSource> _lastBitmap = new(default);

        public Project SelectedProject { get; set; }
        public AvaloniaPreviewer()
        {
            InitializeComponent();
            Update(null);

            Loaded += AvaloniaPreviewer_Loaded;

            buildButton.Click += BuildButton_Click;
            previewScroller.ScrollChanged += PreviewScroller_ScrollChanged;

            SizeChanged += (_, _) => _lastSize = default;
        }

        private async void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = (DTE)Package.GetGlobalService(typeof(DTE));
            var solutionBuild = dte.Solution.SolutionBuild;
            solutionBuild.BuildProject(solutionBuild.ActiveConfiguration.Name, SelectedProject.UniqueName);
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
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var designer = FindParent<AvaloniaDesigner>(this);

                if (designer.TryProcessZoomLevelValue(out var currentZoomLevel))
                {
                    currentZoomLevel += e.Delta > 0 ? 0.25 : -0.25;

                    if (currentZoomLevel < 0.125)
                    {
                        currentZoomLevel = 0.125;
                    }
                    else if (currentZoomLevel > 8)
                    {
                        currentZoomLevel = 8;
                    }

                    designer.ZoomLevel = ZoomLevels.FmtZoomLevel(currentZoomLevel * 100);

                    e.Handled = true;
                }
            }

            base.OnPreviewMouseWheel(e);
        }

        private double GetScaling()
        {
            var result = (Process?.Scaling ?? 1) / VisualTreeHelper.GetDpi(this).DpiScaleX;
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
            if (Process is null)
            {
                return;
            }
            if (bitmap is null)
            {
                _lastBitmap.TryGetTarget(out bitmap);
            }

            preview.Source = bitmap;

            if (bitmap is not null)
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
                    error.Visibility = Visibility.Collapsed;
                    previewScroller.Visibility = Visibility.Visible;
                }

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
                _lastBitmap.SetTarget(bitmap);
            }
        }

        private void PreviewScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // We can't do this in Update because the Scroll info may not be updated 
            // yet and the scrollable size may still be old
            if (_centerPreviewer)
            {
                if (_lastBitmapSize is { } size && size.Width < e.ViewportWidth && size.Height < e.ViewportHeight)
                {
                    previewScroller.ScrollToVerticalOffset(previewScroller.ScrollableHeight / 2);
                }
                else
                {
                    var transform = preview.TransformToVisual(previewScroller);
                    var positionInScrollViewer = transform.TransformBounds(new Rect(0, 0, preview.ActualHeight, preview.ActualHeight));
                    var offset = positionInScrollViewer.Top + e.VerticalOffset;
                    previewScroller.ScrollToVerticalOffset(offset);
                }
                previewScroller.ScrollToHorizontalOffset(previewScroller.ScrollableWidth / 2);
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
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null)
                return null;

            //check if the parent matches the type we're looking for
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        private static AvMouseButton GetButton(WpfMouseButton button)
        {
            switch (button)
            {
                case WpfMouseButton.Left:
                    return AvMouseButton.Left;
                case WpfMouseButton.Middle:
                    return AvMouseButton.Middle;
                case WpfMouseButton.Right:
                    return AvMouseButton.Right;
                default:
                    return AvMouseButton.None;
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

        ScrollBar? _horizontalScroll;
        ScrollBar _verticalScroll;
        Size? _lastSize = default;
        public Size GetViewportSize(int padding)
        {
            if (_lastSize is null)
            {
                var height = previewScroller.ActualHeight;
                var width = previewScroller.ActualWidth;
                if (previewScroller.ComputedHorizontalScrollBarVisibility == Visibility.Visible)
                {
                    if (_horizontalScroll is null)
                    {
                        _horizontalScroll = previewScroller.FindDescendants<ScrollBar>()
                            .First(b => b.Orientation == Orientation.Horizontal);
                    }
                    height -= _horizontalScroll.Height;
                }
                if (previewScroller.ComputedVerticalScrollBarVisibility == Visibility.Visible)
                {
                    if (_verticalScroll == null)
                    {
                        _verticalScroll = previewScroller.FindDescendants<ScrollBar>()
                            .First(b => b.Orientation == Orientation.Vertical);
                    }
                    width -= _verticalScroll.Width;
                }
                _lastSize = new(width - padding * 2, height - padding * 2);
            }
            return _lastSize.Value;
        }


    }
}
