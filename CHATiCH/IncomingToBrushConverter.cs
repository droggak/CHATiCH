using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CHATiCH
{
    /// <summary>
    /// Конвертер: входящее/исходящее сообщение → цвет текста
    /// </summary>
    public class IncomingToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isIncoming)
                return isIncoming ? Brushes.DarkBlue : Brushes.DarkGreen;

            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
