using System;
using System.ComponentModel;

namespace CHATiCH
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private MessageStatus _status;
        private bool _isIncoming;

        public string Id { get; set; } // может быть null
        public string Author { get; set; }
        public string Text { get; set; }
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

        // Для биндинга: разные тексты для входящих и исходящих
        public string ReceiptText
        {
            get
            {
                if (IsIncoming)
                {
                    // входящее: • = не прочитано, "" = прочитано
                    return Status == MessageStatus.Read ? "" : "•";
                }
                else
                {
                    // исходящее: • Отправлено или ✔ Прочитано
                    return Status == MessageStatus.Read ? "✔ Прочитано" : "• Отправлено";
                }
            }
        }

        public override string ToString()
        {
            string who = IsIncoming ? Author : "Я";
            return $"{who}: {Text} [{Time:HH:mm}] ({Status})";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
