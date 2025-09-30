using System.Windows;
using System.Windows.Controls;

namespace CHATiCH
{
    public class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate MarkdownTemplate { get; set; }
        public DataTemplate FileLinkTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMessage msg)
            {
                if (!string.IsNullOrEmpty(msg.Text) &&
                    msg.Text.StartsWith("[") &&
                    msg.Text.Contains("](") &&
                    msg.Text.EndsWith(")"))
                {
                    return FileLinkTemplate;
                }
            }
            return MarkdownTemplate;
        }
    }
}
