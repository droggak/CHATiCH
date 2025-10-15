using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace CHATiCH
{
    public partial class SelectContactWindow : Window
    {
        private ObservableCollection<ChatWindow.ContactGroup> _groups;
        public string SelectedJid { get; private set; }

        public SelectContactWindow(ObservableCollection<ChatWindow.ContactGroup> groups)
        {
            InitializeComponent();
            _groups = groups;

            // заполняем ListBox всеми контактами из групп
            var allContacts = _groups.SelectMany(g => g.Contacts).ToList();
            ContactsList.ItemsSource = allContacts;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (ContactsList.SelectedItem is UserContact contact)
            {
                SelectedJid = contact.Jid;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Выберите контакт из списка.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string search = SearchBox.Text.ToLower();
            var filtered = _groups.SelectMany(g => g.Contacts)
                                  .Where(c => c.Name.ToLower().Contains(search) || c.Jid.ToLower().Contains(search))
                                  .ToList();
            ContactsList.ItemsSource = filtered;
        }
    }
}
