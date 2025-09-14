using S22.Xmpp;
using S22.Xmpp.Im;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CHATiCH
{
    public class UserContact : INotifyPropertyChanged
    {
        private string _jid;
        private string _name;
        private Availability _availability;
        private string _statusText;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Jid
        {
            get => _jid;
            set { _jid = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public Availability Availability
        {
            get => _availability;
            set { _availability = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}