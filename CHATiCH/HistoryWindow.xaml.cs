using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

namespace CHATiCH
{
    public partial class HistoryWindow : Window
    {
        public string Jid { get; set; }

        private ObservableCollection<ChatMessage> _allMessages;
        public ObservableCollection<ChatMessage> FilteredMessages { get; set; }

        public HistoryWindow(string jid)
        {
            InitializeComponent();

            // 🔹 Инициализация с защитой
            Jid = jid ?? string.Empty;
            _allMessages = new ObservableCollection<ChatMessage>();
            FilteredMessages = new ObservableCollection<ChatMessage>();
            DataContext = this;
        }

        // 🔹 Обработчик выбора даты
        private void DateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // 🔹 Обработчик выбора типа сообщений
        private void TypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // 🔹 Фильтрация по дате и типу
        private void ApplyFilters()
        {
            try
            {
                if (DateCalendar == null || TypeFilter == null)
                    return;

                if (FilteredMessages == null)
                    FilteredMessages = new ObservableCollection<ChatMessage>();

                if (DateCalendar.SelectedDate == null)
                {
                    FilteredMessages.Clear();
                    UpdateEmptyLabel();
                    return;
                }

                DateTime selectedDate = DateCalendar.SelectedDate.Value;

                // Тип сообщений
                string type = "all";
                var selectedItem = TypeFilter.SelectedItem as ComboBoxItem;
                if (selectedItem != null && selectedItem.Tag != null)
                    type = selectedItem.Tag.ToString();

                // Загрузка истории
                var hist = HistoryManager.LoadHistory(Jid, selectedDate);
                if (hist == null)
                    hist = new ObservableCollection<ChatMessage>();

                _allMessages.Clear();
                foreach (var msg in hist)
                    _allMessages.Add(msg);

                var filtered = _allMessages.Where(m =>
                {
                    if (m == null)
                        return false;

                    bool matchType = true;
                    if (type == "incoming")
                        matchType = m.IsIncoming;
                    else if (type == "outgoing")
                        matchType = !m.IsIncoming;

                    return matchType;
                }).ToList();

                FilteredMessages.Clear();
                foreach (var msg in filtered)
                    FilteredMessages.Add(msg);

                UpdateEmptyLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке истории:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 🔹 Обновление надписи о пустом списке
        private void UpdateEmptyLabel()
        {
            if (EmptyLabel == null)
                return;

            if (FilteredMessages == null || FilteredMessages.Count == 0)
                EmptyLabel.Visibility = Visibility.Visible;
            else
                EmptyLabel.Visibility = Visibility.Collapsed;
        }

        // 🔹 Экспорт сообщений
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FilteredMessages == null || FilteredMessages.Count == 0)
                {
                    MessageBox.Show("Нет сообщений для экспорта.",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Text file (*.txt)|*.txt|PDF file (*.pdf)|*.pdf"
                };

                bool? result = dialog.ShowDialog();
                if (result == true)
                {
                    if (dialog.FileName.EndsWith(".txt"))
                    {
                        string content = string.Join(Environment.NewLine,
                            FilteredMessages.Select(m =>
                                string.Format("{0:dd.MM.yyyy HH:mm} | {1}: {2}",
                                m.Time, m.Author, m.Text))
                        );
                        File.WriteAllText(dialog.FileName, content);
                    }
                    else if (dialog.FileName.EndsWith(".pdf"))
                    {
                        using (var writer = new PdfWriter(dialog.FileName))
                        using (var pdf = new PdfDocument(writer))
                        using (var doc = new Document(pdf))
                        {
                            foreach (var msg in FilteredMessages)
                            {
                                string line = string.Format("{0:dd.MM.yyyy HH:mm} | {1}: {2}",
                                    msg.Time, msg.Author, msg.Text);
                                doc.Add(new Paragraph(line));
                            }
                        }
                    }

                    MessageBox.Show("Экспорт завершён!",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при экспорте:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
