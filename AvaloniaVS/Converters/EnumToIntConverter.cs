using System;
using System.Globalization;
using System.Windows.Data;

namespace AvaloniaVS.Converters
{
    public class EnumToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType.IsEnum && value is int i)
            {
                return Enum.ToObject(targetType, value);
            }
            else if (value?.GetType().IsEnum == true && targetType == typeof(int))
            {
                return (int)value;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(value, targetType, parameter, culture);
        }
    }
}
