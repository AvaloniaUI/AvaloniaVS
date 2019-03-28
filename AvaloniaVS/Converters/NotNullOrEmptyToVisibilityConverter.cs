using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AvaloniaVS.Converters
{
    internal class NotNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as IList)?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
