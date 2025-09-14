using System;
using System.Globalization;
using System.Windows.Data;

namespace CHATiCH
{
    public class MessageStatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MessageStatus status)
            {
                switch (status)
                {
                    case MessageStatus.Sent:
                        return "• Отправлено";
                    case MessageStatus.Read:
                        return "✔ Прочитано";
                    default:
                        return "";
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
