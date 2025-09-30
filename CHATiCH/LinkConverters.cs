using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace CHATiCH
{
    public class LinkExtractor : IValueConverter
    {
        private static readonly Regex regex = new Regex(@"\[(.*?)\]\((.*?)\)");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text)) return null;

            var match = regex.Match(text);
            return match.Success ? match.Groups[2].Value : text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class LinkTextExtractor : IValueConverter
    {
        private static readonly Regex regex = new Regex(@"\[(.*?)\]\((.*?)\)");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text)) return text;

            var match = regex.Match(text);
            return match.Success ? match.Groups[1].Value : text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
