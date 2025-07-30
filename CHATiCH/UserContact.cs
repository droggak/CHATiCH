using System.ComponentModel;
using S22.Xmpp.Im;

public class UserContact : INotifyPropertyChanged
{
    public string Jid { get; set; }
    public string Name { get; set; }

    private Availability _availability;
    public Availability Availability
    {
        get => _availability;
        set
        {
            _availability = value;
            OnPropertyChanged(nameof(Availability));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
