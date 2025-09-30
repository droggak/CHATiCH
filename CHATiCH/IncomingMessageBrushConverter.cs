using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CHATiCH
{
    public class IncomingMessageBrushConverter : IValueConverter
    {
        // isIncoming = true => входящее сообщение (серое)
        // isIncoming = false => исходящее сообщение (голубое)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isIncoming)
                return isIncoming ? Brushes.LightGray : Brushes.LightBlue;
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
