

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IndustrialControlApp.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, 
            object parameter, CultureInfo culture)
        {
            if (value is bool status)
            {
                return status ? 
                    new SolidColorBrush(Colors.LimeGreen) : 
                    new SolidColorBrush(Colors.Red);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, 
            object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}