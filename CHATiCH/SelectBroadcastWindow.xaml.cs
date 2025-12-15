using S22.Xmpp;
using S22.Xmpp.Client;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using static CHATiCH.ChatWindow;

namespace CHATiCH
{
    public partial class SelectBroadcastWindow : Window
    {
        private readonly XmppClient _client;

        public ObservableCollection<ContactGroup> ContactGroups { get; }

        public SelectBroadcastWindow(
            ObservableCollection<ContactGroup> originalGroups,
            XmppClient client)
        {
            InitializeComponent();

            _client = client;

            // Делаем копию групп и контактов, чтобы чекбоксы не влияли на основной UI
            ContactGroups = new ObservableCollection<ContactGroup>(
                originalGroups.Select(g => new ContactGroup
                {
                    Name = g.Name,
                    Contacts = new ObservableCollection<UserContact>(
                        g.Contacts.Select(c => new UserContact
                        {
                            Name = c.Name,
                            Jid = c.Jid,
                            IsSelected = false
                        })
                    )
                })
            );

            DataContext = this;
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            string text = MessageTextBox.Text.Trim();

            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("Введите текст сообщения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Выбранные пользователи
            var recipients = ContactGroups
                .SelectMany(g => g.Contacts)
                .Where(c => c.IsSelected)
                .ToList();

            if (recipients.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одного пользователя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int sent = 0;
            foreach (var contact in recipients)
            {
                try
                {
                    _client.SendMessage(new Jid(contact.Jid), text);
                    sent++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка отправки {contact.Jid}:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            MessageBox.Show($"Отправлено: {sent} сообщения(й).", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);

            Close();
        }
    }
}
