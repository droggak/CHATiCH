using S22.Xmpp.Im;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CHATiCH
{
    /// <summary>
    /// Конвертер: Availability (Online/Away/Offline/DoNotDisturb) → цвет кружка
    /// </summary>
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Availability availability)
            {
                switch (availability)
                {
                    case Availability.Online:
                        return Brushes.Green;
                    case Availability.Away:
                        return Brushes.Goldenrod;
                    case Availability.DoNotDisturb:
                        return Brushes.Red;
                    case Availability.Offline:
                        return Brushes.Gray;
                    default:
                        return Brushes.Gray;
                }
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
