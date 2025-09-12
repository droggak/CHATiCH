using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace CHATiCH
{
    public partial class HistoryWindow : Window
    {
        private readonly string _jid;

        public ObservableCollection<ChatMessage> Messages { get; set; } = new ObservableCollection<ChatMessage>();

        public HistoryWindow(string jid)
        {
            InitializeComponent();
            _jid = jid;

            DataContext = this;

            LoadAvailableDates();
        }

        private void LoadAvailableDates()
        {
            try
            {
                var dates = HistoryManager.GetAvailableDates(_jid);
                DatesList.ItemsSource = dates;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка дат: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DatesList.SelectedItem is string dateStr)
            {
                try
                {
                    if (DateTime.TryParse(dateStr, out var date))
                    {
                        Messages.Clear();
                        var history = HistoryManager.LoadHistory(_jid, date);
                        foreach (var msg in history)
                            Messages.Add(msg);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке истории: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
