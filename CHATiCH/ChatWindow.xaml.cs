using S22.Xmpp;
using S22.Xmpp.Client;
using S22.Xmpp.Im;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CHATiCH
{
    public partial class ChatWindow : Window
    {
        private XmppClient _client;
        private DispatcherTimer _statusTimer;
        private DateTime _lastActivityTime;
        private bool _manualStatusSet = false;

        public ObservableCollection<UserContact> Contacts { get; set; } = new ObservableCollection<UserContact>();
        public ObservableCollection<ChatTab> ChatTabsItems { get; set; } = new ObservableCollection<ChatTab>();

        public ICommand CloseTabCommand { get; }

        private string HistoryRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CHATiCH", "History");

        public ChatWindow(XmppClient client)
        {
            InitializeComponent();
            _client = client;
            DataContext = this;

            ContactsList.ItemsSource = Contacts;
            ChatTabs.ItemsSource = ChatTabsItems;

            CloseTabCommand = new RelayCommand<ChatTab>(CloseTab);

            LoadRoster();

            _client.StatusChanged += OnStatusChanged;
            _client.Message += OnMessageReceived;
            _client.RosterUpdated += OnRosterUpdated;

            _lastActivityTime = DateTime.Now;

            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(10);
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();

            InputManager.Current.PreProcessInput += OnActivity;
            Closing += ChatWindow_Closing;

            if (StatusComboBox != null && StatusComboBox.Items.Count > 0)
                StatusComboBox.SelectedIndex = 0;

            UpdateStatus(Availability.Online, StatusMessageBox != null ? (StatusMessageBox.Text ?? "") : "Online via WPF");
        }

        private void ChatWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_client != null && _client.Connected)
                    _client.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отключении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === История ===
        private string GetHistoryFile(string jid)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string folder = Path.Combine(HistoryRoot, today);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return Path.Combine(folder, $"{jid.Replace("@", "_at_")}.json");
        }

        private ObservableCollection<ChatMessage> LoadHistory(string jid)
        {
            try
            {
                string file = GetHistoryFile(jid);
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    var history = JsonSerializer.Deserialize<ObservableCollection<ChatMessage>>(json);
                    return history ?? new ObservableCollection<ChatMessage>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка загрузки истории: " + ex.Message);
            }
            return new ObservableCollection<ChatMessage>();
        }

        private void SaveHistory(string jid, ObservableCollection<ChatMessage> messages)
        {
            try
            {
                string file = GetHistoryFile(jid);
                string json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка сохранения истории: " + ex.Message);
            }
        }

        // === Активность пользователя ===
        private void OnActivity(object sender, PreProcessInputEventArgs e)
        {
            _lastActivityTime = DateTime.Now;
            if (!_manualStatusSet && StatusComboBox != null)
            {
                var selected = StatusComboBox.SelectedItem as ComboBoxItem;
                if (selected != null && selected.Tag != null && selected.Tag.ToString() == "Away")
                {
                    StatusComboBox.SelectedIndex = 0;
                    UpdateStatus(Availability.Online, StatusMessageBox != null ? (StatusMessageBox.Text ?? "") : "Online via WPF");
                }
            }
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!_manualStatusSet)
                {
                    var idleTime = DateTime.Now - _lastActivityTime;
                    if (idleTime.TotalMinutes >= 5)
                    {
                        if (StatusComboBox != null && StatusComboBox.SelectedIndex != 1)
                        {
                            StatusComboBox.SelectedIndex = 1;
                            UpdateStatus(Availability.Away, StatusMessageBox != null ? (StatusMessageBox.Text ?? "Отошел") : "Отошел");
                        }
                    }
                    else
                    {
                        if (StatusComboBox != null)
                        {
                            var sel = StatusComboBox.SelectedItem as ComboBoxItem;
                            if (sel != null && sel.Tag != null && sel.Tag.ToString() == "Away")
                            {
                                StatusComboBox.SelectedIndex = 0;
                                UpdateStatus(Availability.Online, StatusMessageBox != null ? (StatusMessageBox.Text ?? "") : "Online via WPF");
                            }
                        }
                    }
                }

                var currentAvailability = GetUiSelectedAvailability();
                var currentStatusText = StatusMessageBox != null ? (StatusMessageBox.Text ?? "") : "";
                UpdateStatus(currentAvailability, currentStatusText);

                var roster = _client.GetRoster();
                Dispatcher.Invoke(() =>
                {
                    foreach (var item in roster)
                    {
                        var bareJid = GetBareJid(item.Jid);
                        var contact = Contacts.FirstOrDefault(c => c.Jid == bareJid);
                        if (contact == null)
                        {
                            Contacts.Add(new UserContact
                            {
                                Jid = bareJid,
                                Name = string.IsNullOrEmpty(item.Name) ? bareJid : item.Name,
                                Availability = Availability.Offline,
                                StatusText = "Неизвестен"
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в StatusTimer_Tick: {ex.Message}");
            }
        }

        private Availability GetUiSelectedAvailability()
        {
            try
            {
                var selected = StatusComboBox != null ? StatusComboBox.SelectedItem as ComboBoxItem : null;
                var tag = selected != null ? selected.Tag as string : null;
                if (string.Equals(tag, "Away", StringComparison.OrdinalIgnoreCase))
                    return Availability.Away;
                return Availability.Online;
            }
            catch
            {
                return Availability.Online;
            }
        }

        // === Работа с ростером ===
        private void LoadRoster()
        {
            try
            {
                var roster = _client.GetRoster();
                Dispatcher.Invoke(() =>
                {
                    foreach (var item in roster)
                    {
                        var bareJid = GetBareJid(item.Jid);
                        var contact = Contacts.FirstOrDefault(c => c.Jid == bareJid);
                        if (contact == null)
                        {
                            Contacts.Add(new UserContact
                            {
                                Jid = bareJid,
                                Name = string.IsNullOrEmpty(item.Name) ? bareJid : item.Name,
                                Availability = Availability.Offline,
                                StatusText = "Неизвестен"
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке ростера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnStatusChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var args = e as StatusEventArgs;
                if (args != null)
                {
                    var bareJid = GetBareJid(args.Jid);
                    var contact = Contacts.FirstOrDefault(c => c.Jid == bareJid);
                    if (contact != null)
                    {
                        contact.Availability = args.Status.Availability;
                        contact.StatusText = args.Status.Message ?? "Неизвестен";
                        ContactsList.Items.Refresh();
                    }
                }
            });
        }

        private void OnRosterUpdated(object sender, EventArgs e)
        {
            Dispatcher.Invoke(LoadRoster);
        }

        // === Сообщения ===
        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string bareJid = GetBareJid(e.Jid);
                var tab = ChatTabsItems.FirstOrDefault(t => t.Jid == bareJid);
                if (tab == null)
                {
                    var messages = LoadHistory(bareJid);
                    messages.Add(new ChatMessage { Author = bareJid, Text = e.Message.Body, Time = DateTime.Now, IsIncoming = true });
                    tab = new ChatTab { Jid = bareJid, Header = bareJid, Content = messages };
                    ChatTabsItems.Add(tab);
                }
                else
                {
                    var existing = tab.Content as ObservableCollection<ChatMessage>;
                    if (existing != null)
                    {
                        existing.Add(new ChatMessage { Author = bareJid, Text = e.Message.Body, Time = DateTime.Now, IsIncoming = true });
                        SaveHistory(bareJid, existing);
                    }
                }
            });
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (_client == null || !_client.Connected)
            {
                MessageBox.Show("Клиент не подключён к серверу.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var tab = ChatTabs.SelectedItem as ChatTab;
            var messages = tab != null ? tab.Content as ObservableCollection<ChatMessage> : null;

            if (tab != null && messages != null)
            {
                if (!string.IsNullOrWhiteSpace(MessageTextBox.Text))
                {
                    try
                    {
                        _client.SendMessage(new Jid(tab.Jid), MessageTextBox.Text);
                        messages.Add(new ChatMessage { Author = "Я", Text = MessageTextBox.Text, Time = DateTime.Now, IsIncoming = false });
                        SaveHistory(tab.Jid, messages);
                        MessageTextBox.Clear();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка при отправке сообщения: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите вкладку для отправки сообщения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SendMessage_Click(sender, new RoutedEventArgs());
        }

        private void ContactsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var contact = ContactsList.SelectedItem as UserContact;
            if (contact != null)
            {
                var existingTab = ChatTabsItems.FirstOrDefault(t => t.Jid == contact.Jid);
                if (existingTab == null)
                {
                    var messages = LoadHistory(contact.Jid);
                    var newTab = new ChatTab { Jid = contact.Jid, Header = contact.Name, Content = messages };
                    ChatTabsItems.Add(newTab);
                    ChatTabs.SelectedItem = newTab;
                }
                else
                {
                    ChatTabs.SelectedItem = existingTab;
                }
            }
        }

        private void MessagesList_Loaded(object sender, RoutedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox != null && listBox.Items.Count > 0)
                listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
        }

        private string GetBareJid(Jid jid) => $"{jid.Node}@{jid.Domain}";

        private void CloseTab(ChatTab tab)
        {
            if (tab != null && ChatTabsItems.Contains(tab))
                ChatTabsItems.Remove(tab);
        }

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = StatusComboBox != null ? StatusComboBox.SelectedItem as ComboBoxItem : null;
            if (selected != null && selected.Tag != null)
            {
                _manualStatusSet = true;
                var tag = selected.Tag.ToString();
                Availability availability = (string.Equals(tag, "Away", StringComparison.OrdinalIgnoreCase))
                    ? Availability.Away
                    : Availability.Online;

                UpdateStatus(availability, StatusMessageBox != null ? (StatusMessageBox.Text ?? "") : "");
            }
        }

        private void StatusMessageBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var selected = StatusComboBox != null ? StatusComboBox.SelectedItem as ComboBoxItem : null;
            var tag = selected != null ? selected.Tag as string : null;
            Availability availability = (string.Equals(tag, "Away", StringComparison.OrdinalIgnoreCase))
                ? Availability.Away
                : Availability.Online;

            UpdateStatus(availability, StatusMessageBox != null ? (StatusMessageBox.Text ?? "") : "");
        }

        private void UpdateStatus(Availability availability, string statusText)
        {
            try
            {
                if (_client != null && _client.Connected)
                    _client.SetStatus(availability, statusText ?? "");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при обновлении статуса: " + ex.Message);
            }
        }
    }

    // === Модели и конвертеры ===
    public class ChatMessage
    {
        public string Author { get; set; }
        public string Text { get; set; }
        public DateTime Time { get; set; }
        public bool IsIncoming { get; set; }

        public override string ToString()
        {
            string who = IsIncoming ? Author : "Я";
            return $"{who}: {Text} [{Time:HH:mm}]";
        }
    }

    public class ChatTab
    {
        public string Header { get; set; }
        public object Content { get; set; }
        public string Jid { get; set; }
    }

    

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)parameter);
        public void Execute(object parameter) => _execute((T)parameter);

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }

    
}
