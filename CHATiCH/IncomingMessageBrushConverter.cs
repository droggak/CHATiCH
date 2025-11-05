using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CHATiCH
{
    public class IncomingMessageBrushConverter : IValueConverter
    {
        // isIncoming = true => входящее сообщение (серое)
        // isIncoming = false => исходящее сообщение (темно-серое)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isIncoming)
            {
                // Используем стандартные кисти WPF:
                // Входящее: LightGray/Gray (для фона)
                // Исходящее: DarkGray/Black (для фона)
                return isIncoming ? Brushes.SlateGray : Brushes.LightSlateGray;
            }

            return Brushes.Transparent; // Используем Transparent вместо White
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}