using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace AvaloniaVS.Views
{
    public static class VsTheme
    {
        private static Dictionary<UIElement, bool> _isUsingVsTheme = new Dictionary<UIElement, bool>();
        private static Dictionary<UIElement, object> _originalBackgrounds = new Dictionary<UIElement, object>();

        public static DependencyProperty UseVsThemeProperty = DependencyProperty.RegisterAttached("UseVsTheme", typeof(bool), typeof(VsTheme), new PropertyMetadata(false, UseVsThemePropertyChanged));

        private static void UseVsThemePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SetUseVsTheme((UIElement)d, (bool)e.NewValue);
        }

        public static void SetUseVsTheme(UIElement element, bool value)
        {
            if (value)
            {
                if (!_originalBackgrounds.ContainsKey(element) && element is Control c)
                {
                    _originalBackgrounds[element] = c.Background;
                }

                ((FrameworkElement)element).ShouldBeThemed();
            }
            else
            {
                ((FrameworkElement)element).ShouldNotBeThemed();
            }

            _isUsingVsTheme[element] = value;
        }

        public static bool GetUseVsTheme(UIElement element)
        {
            return _isUsingVsTheme.TryGetValue(element, out bool value) && value;
        }

        private static ResourceDictionary BuildThemeResources()
        {
            var allResources = new ResourceDictionary();

            try
            {
                var shellResources = (ResourceDictionary)Application.LoadComponent(new Uri("Microsoft.VisualStudio.Platform.WindowManagement;component/Themes/ThemedDialogDefaultStyles.xaml", UriKind.Relative));
                var scrollStyleContainer = (ResourceDictionary)Application.LoadComponent(new Uri("Microsoft.VisualStudio.Shell.UI.Internal;component/Styles/ScrollBarStyle.xaml", UriKind.Relative));
                allResources.MergedDictionaries.Add(shellResources);
                allResources.MergedDictionaries.Add(scrollStyleContainer);
                allResources[typeof(ScrollViewer)] = new Style
                {
                    TargetType = typeof(ScrollViewer),
                    BasedOn = (Style)scrollStyleContainer[VsResourceKeys.ScrollViewerStyleKey]
                };

                allResources[typeof(TextBox)] = new Style
                {
                    TargetType = typeof(TextBox),
                    BasedOn = (Style)shellResources[typeof(TextBox)],
                    Setters =
                    {
                        new Setter(Control.PaddingProperty, new Thickness(2, 3, 2, 3))
                    }
                };

                allResources[typeof(ComboBox)] = new Style
                {
                    TargetType = typeof(ComboBox),
                    BasedOn = (Style)shellResources[typeof(ComboBox)],
                    Setters =
                    {
                        new Setter(Control.PaddingProperty, new Thickness(2, 3, 2, 3))
                    }
                };
            }
            catch
            { }

            return allResources;
        }

        private static ResourceDictionary ThemeResources { get; } = BuildThemeResources();

        private static void ShouldBeThemed(this FrameworkElement control)
        {
            if (control.Resources == null)
            {
                control.Resources = ThemeResources;
            }
            else if (control.Resources != ThemeResources)
            {
                var d = new ResourceDictionary();
                d.MergedDictionaries.Add(ThemeResources);
                d.MergedDictionaries.Add(control.Resources);
                control.Resources = null;
                control.Resources = d;
            }

            if (control is Control c)
            {
                c.SetResourceReference(Control.BackgroundProperty, (string)EnvironmentColors.StartPageTabBackgroundBrushKey);
            }
        }

        private static void ShouldNotBeThemed(this FrameworkElement control)
        {
            if (control.Resources != null)
            {
                if (control.Resources == ThemeResources)
                {
                    control.Resources = new ResourceDictionary();
                }
                else
                {
                    control.Resources.MergedDictionaries.Remove(ThemeResources);
                }
            }

            //If we're themed now and we're something with a background property, reset it
            if (GetUseVsTheme(control) && control is Control c)
            {
                if (_originalBackgrounds.TryGetValue(control, out object background))
                {
                    c.SetValue(Control.BackgroundProperty, background);
                }
                else
                {
                    c.ClearValue(Control.BackgroundProperty);
                }
            }
        }
    }
}
