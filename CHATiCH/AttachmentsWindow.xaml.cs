using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;

namespace CHATiCH
{
    public partial class AttachmentsWindow : Window
    {
        private ObservableCollection<ChatMessage> _allMessages;
        private ObservableCollection<ChatMessage> _filteredMessages;

        private string _jid; // 🧩 идентификатор собеседника

        public AttachmentsWindow(string jid)
        {
            InitializeComponent();
            _jid = jid ?? string.Empty;
            LoadAttachments();
        }

        private void LoadAttachments()
        {
            try
            {
                // 🧩 Загружаем ВСЮ историю сообщений (не только за сегодня)
                var allHistory = HistoryManager.LoadAllHistory(_jid);
                if (allHistory == null)
                    allHistory = new ObservableCollection<ChatMessage>();

                // 🧹 Берём только сообщения с файлами
                var fileMessages = allHistory
                    .Where(m => m != null && !string.IsNullOrEmpty(m.FileUrl))
                    .ToList();

                _allMessages = new ObservableCollection<ChatMessage>(fileMessages);
                _filteredMessages = new ObservableCollection<ChatMessage>(_allMessages);

                AttachmentsList.ItemsSource = _filteredMessages;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке вложений:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DateFilter_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void TypeFilter_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_allMessages == null)
                return;

            var selectedDate = DateFilter.SelectedDate;
            var type = (TypeFilter.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString();

            var filtered = _allMessages
                .Where(m => m != null && !string.IsNullOrEmpty(m.FileUrl))
                .Where(m =>
                {
                    bool dateMatch = !selectedDate.HasValue || m.Time.Date == selectedDate.Value.Date;
                    bool typeMatch = type == "all" || GetFileType(m.FileName) == type;
                    return dateMatch && typeMatch;
                })
                .ToList();

            _filteredMessages.Clear();
            foreach (var msg in filtered)
                _filteredMessages.Add(msg);
        }

        private string GetFileType(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "other";
            string ext = System.IO.Path.GetExtension(fileName).ToLower();
            if (new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" }.Contains(ext)) return "image";
            if (new[] { ".mp4", ".avi", ".mov", ".mkv" }.Contains(ext)) return "video";
            if (new[] { ".mp3", ".wav", ".flac" }.Contains(ext)) return "audio";
            if (new[] { ".doc", ".docx", ".pdf", ".xls", ".xlsx", ".txt" }.Contains(ext)) return "doc";
            if (new[] { ".zip", ".rar", ".7z" }.Contains(ext)) return "archive";
            return "other";
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при открытии ссылки: " + ex.Message);
            }
            e.Handled = true;
        }
    }
}
