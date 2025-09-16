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
        public string Text { get; set; }
        public DateTime Time { get; set; }

        public bool IsIncoming
        {
            get { return _isIncoming; }
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
            get { return _status; }
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

        public Brush AuthorBrush
        {
            get { return IsIncoming ? Brushes.Blue : Brushes.Green; }
        }

        public string ReceiptText
        {
            get
            {
                if (IsIncoming)
                {
                    return Status == MessageStatus.Read ? "" : "•";
                }
                else
                {
                    return Status == MessageStatus.Read ? "✔ Прочитано" : "• Отправлено";
                }
            }
        }

        public override string ToString()
        {
            string who = IsIncoming ? Author : "Я";
            return string.Format("{0}: {1} [{2:HH:mm}] ({3})", who, Text, Time, Status);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}