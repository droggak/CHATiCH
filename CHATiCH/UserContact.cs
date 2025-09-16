using S22.Xmpp;
using S22.Xmpp.Im;
using System.ComponentModel;
using System.Windows.Media;

namespace CHATiCH
{
    public class UserContact : INotifyPropertyChanged
    {
        private Availability _availability;
        private string _statusText;

        public string Jid { get; set; }
        public string Name { get; set; }

        public Availability Availability
        {
            get { return _availability; }
            set
            {
                if (_availability != value)
                {
                    _availability = value;
                    OnPropertyChanged(nameof(Availability));
                    OnPropertyChanged(nameof(StatusBrush));
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

        // === Новый код ===
        public Brush StatusBrush
        {
            get
            {
                switch (Availability)
                {
                    case Availability.Online:
                        return Brushes.Green;
                    case Availability.Away:
                        return Brushes.Orange;
                    case Availability.Offline:
                        return Brushes.Gray;
                    default:
                        return Brushes.Gray;
                }
            }
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
