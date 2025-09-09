using S22.Xmpp.Im;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CHATiCH
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Availability availability)
            {
                switch (availability)
                {
                    case Availability.Online:
                        return new SolidColorBrush(Colors.Green);
                    case Availability.Away:
                        return new SolidColorBrush(Colors.Yellow);
                    case Availability.DoNotDisturb:
                        return new SolidColorBrush(Colors.Red);
                    case Availability.Offline:
                        return new SolidColorBrush(Colors.Gray);
                    default:
                        return new SolidColorBrush(Colors.Gray);
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
