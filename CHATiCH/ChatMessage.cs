using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;

namespace CHATiCH
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private MessageStatus _status;
        private bool _isIncoming;
        private bool _isEdited;
        private ObservableCollection<string> _reactions = new ObservableCollection<string>();

        public string Id { get; set; }
        public string Author { get; set; }
        public string Text { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public DateTime Time { get; set; }
        public string ForwardedFrom { get; set; }
        public string ForwardedText { get; set; }
        public bool IsForwarded => !string.IsNullOrEmpty(ForwardedFrom);


        public bool CanEdit => !IsIncoming && (DateTime.Now - Time).TotalMinutes < 3;

        public bool IsEdited
        {
            get => _isEdited;
            set
            {
                if (_isEdited != value)
                {
                    _isEdited = value;
                    OnPropertyChanged(nameof(IsEdited));
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public ObservableCollection<string> Reactions
        {
            get => _reactions;
            set
            {
                if (_reactions != value)
                {
                    _reactions = value;
                    OnPropertyChanged(nameof(Reactions));
                }
            }
        }

        public void AddReaction(string emoji)
        {
            if (!Reactions.Contains(emoji))
                Reactions.Add(emoji);
        }

        public void RemoveReaction(string emoji)
        {
            if (Reactions.Contains(emoji))
                Reactions.Remove(emoji);
        }

        public string EditedText => IsEdited ? "(отредактировано)" : "";
        public string DisplayText => Text;

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
        public string FileType
        {
            get
            {
                if (string.IsNullOrEmpty(FileName))
                    return "other";

                string ext = System.IO.Path.GetExtension(FileName).ToLower();

                if (new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" }.Contains(ext)) return "image";
                if (new[] { ".mp4", ".avi", ".mov", ".mkv" }.Contains(ext)) return "video";
                if (new[] { ".mp3", ".wav", ".flac" }.Contains(ext)) return "audio";
                if (new[] { ".doc", ".docx", ".pdf", ".xls", ".xlsx", ".txt" }.Contains(ext)) return "doc";
                if (new[] { ".zip", ".rar", ".7z" }.Contains(ext)) return "archive";

                return "other";
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
        public void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
