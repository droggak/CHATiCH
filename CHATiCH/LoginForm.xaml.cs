using S22.Xmpp.Client;
using System;
using System.Windows;

namespace CHATiCH
{
    public partial class LoginForm : Window
    {
        public LoginForm()
        {
            InitializeComponent();
            LoadConfig();
        }
        private void LoadConfig()
        {
            var config = AppConfig.Load();
            if (config != null && config.RememberMe)
            {
                txtUsername.Text = config.Username;
                txtDomain.Text = config.Domain;
                chkRememberMe.IsChecked = config.RememberMe;
                chkRememberPassword.IsChecked = config.RememberPassword;

                if (config.RememberPassword && !string.IsNullOrEmpty(config.EncryptedPassword))
                {
                    txtPassword.Password = AppConfig.Decrypt(config.EncryptedPassword);
                }
            }
        }
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = txtUsername.Text.Trim();
                string password = txtPassword.Password.Trim();
                string domain = txtDomain.Text.Trim();

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(domain))
                {
                    MessageBox.Show("Заполните все поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // создаём XMPP клиент
                var client = new XmppClient(domain, username, password);

                // пробуем подключиться
                client.Connect();
                var config = new AppConfig
                {
                    Username = username,
                    Domain = domain,
                    RememberMe = chkRememberMe.IsChecked == true,
                    RememberPassword = chkRememberPassword.IsChecked == true,
                    EncryptedPassword = chkRememberPassword.IsChecked == true
                        ? AppConfig.Encrypt(password)
                        : null
                };

                config.Save();
                // запускаем чат
                var chatWindow = new ChatWindow(client);
                chatWindow.Show();

                this.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при авторизации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
