using S22.Xmpp;
using S22.Xmpp.Im;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace CHATiCH
{
    public class UserContact : INotifyPropertyChanged
    {
        private Availability _availability;
        private string _statusText;
        private int _unreadCount;

        public string Jid { get; set; }
        public string Name { get; set; }

        public Availability Availability
        {
            get => _availability;
            set
            {
                if (_availability != value)
                {
                    _availability = value;
                    OnPropertyChanged(nameof(Availability));
                    OnPropertyChanged(nameof(StatusBrush)); // уведомляем, что цвет тоже поменялся
                }
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public Brush StatusBrush
        {
            get
            {
                if (Availability == Availability.Online)
                    return Brushes.Green;
                else if (Availability == Availability.Away)
                    return Brushes.Orange;
                else
                    return Brushes.Gray;
            }
        }
        private bool _isFavorite = false;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged(nameof(IsFavorite));
                }
            }
        }
        // ---------------- Новый код для индикатора новых сообщений ----------------
        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                if (_unreadCount != value)
                {
                    _unreadCount = value;
                    OnPropertyChanged(nameof(UnreadCount));
                    OnPropertyChanged(nameof(UnreadIndicatorVisibility));
                }
            }
        }

        public Visibility UnreadIndicatorVisibility => UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        // --------------------------------------------------------------------------

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
