using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Markup;

namespace AvaloniaVS.Converters
{
    public class EnumDescriptionConverter : MarkupExtension, IValueConverter
    {
        public Type EnumType { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (EnumType == null || value == null) {
                return value;
            }

            var fieldInfo = EnumType.GetField(System.Convert.ToString(value));
            var descriptionAttribute = fieldInfo.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault();
            if (descriptionAttribute == null) {
                return value;
            }

            return descriptionAttribute.Description;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // there should be no convert back, make sure the binding is just one-way.
            throw new InvalidOperationException();
        }
    }
}
