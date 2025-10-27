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

            Jid = jid ?? string.Empty;
            _allMessages = new ObservableCollection<ChatMessage>();
            FilteredMessages = new ObservableCollection<ChatMessage>();
            DataContext = this;
        }

        private void DateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void TypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            try
            {
                // 🔒 Безопасность — проверяем элементы
                if (FilteredMessages == null)
                    FilteredMessages = new ObservableCollection<ChatMessage>();

                if (DateCalendar == null || TypeFilter == null)
                    return;

                FilteredMessages.Clear();

                // 🗓 Без выбранной даты — просто очищаем
                if (DateCalendar.SelectedDate == null)
                {
                    UpdateEmptyLabel();
                    return;
                }

                DateTime selectedDate = DateCalendar.SelectedDate.Value;

                // 📤 Тип сообщений
                string type = "all";
                var selectedItem = TypeFilter.SelectedItem as ComboBoxItem;
                if (selectedItem != null && selectedItem.Tag != null)
                    type = selectedItem.Tag.ToString();

                // 🔍 Поисковый запрос
                string search = string.Empty;
                if (SearchBox != null && !string.IsNullOrWhiteSpace(SearchBox.Text))
                    search = SearchBox.Text.Trim().ToLower();

                // 💾 Загружаем историю
                var hist = HistoryManager.LoadHistory(Jid, selectedDate);
                if (hist == null)
                    hist = new ObservableCollection<ChatMessage>();

                _allMessages.Clear();
                foreach (var msg in hist)
                    _allMessages.Add(msg);

                // 🎯 Применяем фильтры
                var filtered = _allMessages.Where(m =>
                {
                    if (m == null)
                        return false;

                    bool matchType =
                        type == "all" ||
                        (type == "incoming" && m.IsIncoming) ||
                        (type == "outgoing" && !m.IsIncoming);

                    bool matchSearch =
                        string.IsNullOrEmpty(search) ||
                        (m.Text != null && m.Text.ToLower().Contains(search)) ||
                        (m.Author != null && m.Author.ToLower().Contains(search));

                    return matchType && matchSearch;
                }).ToList();

                foreach (var msg in filtered)
                    FilteredMessages.Add(msg);

                UpdateEmptyLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при фильтрации истории:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void UpdateEmptyLabel()
        {
            if (EmptyLabel == null)
                return;

            if (FilteredMessages == null || FilteredMessages.Count == 0)
                EmptyLabel.Visibility = Visibility.Visible;
            else
                EmptyLabel.Visibility = Visibility.Collapsed;
        }


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

                if (dialog.ShowDialog() != true)
                    return;

                // 🔧 Преобразование текста (ссылки и эмодзи)
                Func<string, string> normalizeText = (text) =>
                {
                    if (string.IsNullOrEmpty(text))
                        return string.Empty;

                    string processed = text;

                    // 🧩 заменяем эмодзи
                    processed = System.Text.RegularExpressions.Regex.Replace(
                        processed,
                        @"[\uD800-\uDBFF][\uDC00-\uDFFF]",
                        "[эмодзи]"
                    );

                    // 🔗 заменяем ссылки
                    processed = System.Text.RegularExpressions.Regex.Replace(
                        processed,
                        @"(https?://[^\s]+)",
                        "[файл: $1]"
                    );

                    return processed;
                };

                // 📝 Экспорт в TXT
                if (dialog.FileName.EndsWith(".txt"))
                {
                    using (var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        foreach (var msg in FilteredMessages)
                        {
                            string safeText = normalizeText(msg.Text);
                            writer.WriteLine($"{msg.Time:dd.MM.yyyy HH:mm} | {msg.Author}: {safeText}");
                        }
                    }
                }

                // 📄 Экспорт в PDF
                else if (dialog.FileName.EndsWith(".pdf"))
                {
                    using (var writer = new PdfWriter(dialog.FileName))
                    using (var pdf = new PdfDocument(writer))
                    using (var doc = new Document(pdf))
                    {
                        // ✅ Создаём базовые шрифты без третьего аргумента (старый синтаксис)
                        var fontNormal = iText.Kernel.Font.PdfFontFactory.CreateFont(
                            iText.IO.Font.Constants.StandardFonts.HELVETICA,
                            iText.IO.Font.PdfEncodings.WINANSI);
                        var fontBold = iText.Kernel.Font.PdfFontFactory.CreateFont(
                            iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD,
                            iText.IO.Font.PdfEncodings.WINANSI);

                        doc.SetFont(fontNormal);
                        doc.SetFontSize(11);

                        foreach (var msg in FilteredMessages)
                        {
                            string header = $"{msg.Time:dd.MM.yyyy HH:mm} | {msg.Author}:";
                            string safeText = normalizeText(msg.Text);

                            // 🧾 Заголовок (жирный)
                            var headerPara = new Paragraph(header).SetFont(fontBold);
                            doc.Add(headerPara);

                            // ✏️ Текст
                            var textPara = new Paragraph(safeText).SetFont(fontNormal);
                            doc.Add(textPara);

                            // 🔹 Отступ между сообщениями
                            doc.Add(new Paragraph("\n"));
                        }
                    }
                }

                MessageBox.Show("Экспорт завершён!",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при экспорте:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



    }
}