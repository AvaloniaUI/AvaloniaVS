using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AvaloniaVS.Infrastructure
{
    public class TriggerMenuOnLeftClickBehavior
    {
        private static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled", typeof(bool), typeof(TriggerMenuOnLeftClickBehavior),
                new PropertyMetadata(new PropertyChangedCallback(HandlePropertyChanged))
            );

        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

        private static void HandlePropertyChanged(
          DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (obj is ButtonBase el)
            {
                if (args.NewValue as bool? == true)
                    el.Click += OnClick;
                else
                    el.Click -= OnClick;
            }


        }

        private static void OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is ButtonBase btn)
            {
                var menu = btn.ContextMenu;
                if (menu != null)
                {
                    menu.PlacementTarget = btn;
                    menu.DataContext = btn.DataContext;
                    menu.IsOpen = true;
                }
            }
        }
    }
}
