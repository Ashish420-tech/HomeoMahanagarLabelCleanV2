using System;
using System.Globalization;
using System.Windows.Data;

namespace HomeoMahanagarLabelCleanV2.Converters
{
    public class NotNullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = (parameter as string) == "invert";
            bool r = value != null && (!(value is bool b) || b);
            return invert ? !r : r;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
