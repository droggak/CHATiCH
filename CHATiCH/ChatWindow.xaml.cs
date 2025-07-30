using S22.Xmpp;
using S22.Xmpp.Client;
using S22.Xmpp.Im;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace PandionClone
{
    public partial class ChatWindow : Window
    {
        private XmppClient _client;
        private Jid _selectedUser;
        public ObservableCollection<UserContact> Contacts { get; set; } = new ObservableCollection<UserContact>();

        public ChatWindow(XmppClient client)
        {

            InitializeComponent();
            _client = client;

            LoadContacts();

            _client.Message += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_selectedUser != null && GetBareJid(e.Jid) == GetBareJid(_selectedUser))
                    {
                        ChatHistory.Text += $"{e.Jid.Node}: {e.Message}\n";
                        ChatHistory.ScrollToEnd();
                    }
                });
            };

        }

        private void LoadContacts()
        {
            var roster = _client.GetRoster();

            foreach (var contact in roster)
            {
                UsersListBox.Items.Add(contact.Jid.ToString());
            }
        }

        private void UsersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersListBox.SelectedItem != null)
            {
                _selectedUser = new Jid(UsersListBox.SelectedItem.ToString());
                ChatHistory.Text += $"➡️ Общение с: {_selectedUser}\n";
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null)
            {
                MessageBox.Show("Выберите пользователя.");
                return;
            }

            string message = InputBox.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                _client.SendMessage(_selectedUser, message);
                ChatHistory.Text += $"Я: {message}\n";
                ChatHistory.ScrollToEnd();
                InputBox.Clear();
            }
        }

        private string GetBareJid(Jid jid)
        {
            return $"{jid.Node}@{jid.Domain}";
        }
    }
}
