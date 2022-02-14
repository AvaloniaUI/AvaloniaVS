using System;
using System.Globalization;
using System.Windows.Data;

namespace AvaloniaVS.Converters
{
    public class EnumValuesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Type t)
            {
                return Enum.GetValues(t);
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
