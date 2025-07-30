using S22.Xmpp.Client;
using S22.Xmpp.Im;
using System;
using System.Windows;

namespace PandionClone
{
    public partial class LoginForm : Window
    {
        public LoginForm()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string password = txtPassword.Password;
            string domain = txtDomain.Text;

            try
            {
                XmppClient client = new XmppClient(
                    hostname: domain,
                    username: username,
                    password: password,
                    port: 5222,
                    tls: true
                );

                client.Connect();
                client.SetStatus(Availability.Online, "Online via WPF");

                MessageBox.Show("Успешный вход");

                ChatWindow chatWindow = new ChatWindow(client);
                chatWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка авторизации: " + ex.Message);
            }
        }
    }
}
