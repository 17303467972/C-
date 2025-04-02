using System;
using System.Globalization;
using System.Windows.Data;

namespace IndustrialControlApp.Converters
{
    public class MotorStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, 
            object parameter, CultureInfo culture)
        {
            if (value is bool status)
            {
                return status ? "停止电机 (运行中)" : "启动电机 (已停止)";
            }
            return "状态未知";
        }

        public object ConvertBack(object value, Type targetType, 
            object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}