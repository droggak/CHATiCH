using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

namespace CHATiCH
{
    public class EmojiTextConverter : IValueConverter
    {
        // Используем полный синтаксис для Regex, совместимый со старыми версиями C#.
        private static readonly Regex EmojiRegex = new Regex(@"\[emoji:(.*?)\]", RegexOptions.Compiled);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string ?? "";

            // Используем полный синтаксис для TextBlock.
            var textBlock = new TextBlock
            {
                TextWrapping = System.Windows.TextWrapping.Wrap
            };

            int lastIndex = 0;

            foreach (Match match in EmojiRegex.Matches(text))
            {
                if (match.Index > lastIndex)
                {
                    textBlock.Inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
                }

                string path = match.Groups[1].Value;

                var img = new Image
                {
                    Source = new BitmapImage(new Uri(path, UriKind.Relative)),
                    Width = 28,
                    Height = 28
                };

                textBlock.Inlines.Add(new InlineUIContainer(img));
                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                textBlock.Inlines.Add(new Run(text.Substring(lastIndex)));
            }

            return textBlock;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}