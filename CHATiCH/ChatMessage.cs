using System;
using System.ComponentModel;
using System.Windows.Media;

namespace CHATiCH
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private MessageStatus _status;
        private bool _isIncoming;

        public string Id { get; set; }
        public string Author { get; set; }
        public string Text { get; set; }       // Markdown для отображения
        public string FileName { get; set; }   // Отображаемое имя файла
        public string FileUrl { get; set; }    // Чистый URL для скачивания
        public DateTime Time { get; set; }

        public bool IsIncoming
        {
            get => _isIncoming;
            set
            {
                if (_isIncoming != value)
                {
                    _isIncoming = value;
                    OnPropertyChanged(nameof(IsIncoming));
                    OnPropertyChanged(nameof(AuthorBrush));
                    OnPropertyChanged(nameof(ReceiptText));
                }
            }
        }

        public MessageStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(ReceiptText));
                }
            }
        }

        public Brush AuthorBrush => IsIncoming ? Brushes.Blue : Brushes.Green;

        public string ReceiptText => IsIncoming
            ? (Status == MessageStatus.Read ? "" : "•")
            : (Status == MessageStatus.Read ? "✔ Прочитано" : "• Отправлено");

        public override string ToString()
        {
            string who = IsIncoming ? Author : "Я";
            return $"{who}: {Text} [{Time:HH:mm}] ({Status})";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
