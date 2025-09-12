using S22.Xmpp;
using S22.Xmpp.Client;
using S22.Xmpp.Im;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

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

        // Метапрефиксы (совместимо и безопасно для твоей версии S22)
        private const string MetaPrefixId = "##id:";
        private const string MetaPrefixReceipt = "##receipt:"; // ##receipt:{id}##

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

        // ==== история ====
        private string GetHistoryFile(string jid)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CHATiCH", "History", today);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"{jid.Replace("@", "_at_")}.json");
        }

        // ==== активность/таймер/ростер ====
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

        private void OnRosterUpdated(object sender, EventArgs e) => Dispatcher.Invoke(LoadRoster);

        // ==== обработка входящих сообщений и "мета" ====
        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string body = e.Message?.Body ?? string.Empty;
                string bareJid = GetBareJid(e.Jid);

                // 1) receipt ( ##receipt:{id}## ) — приходит от получателя, означает что наш исходящий стал прочитан
                if (body.StartsWith(MetaPrefixReceipt))
                {
                    int start = MetaPrefixReceipt.Length;
                    int end = body.IndexOf("##", start);
                    if (end > start)
                    {
                        string receiptId = body.Substring(start, end - start);
                        // Найти своё исходящее сообщение и отметить его Read
                        foreach (var tab in ChatTabsItems)
                        {
                            var list = tab.Content as ObservableCollection<ChatMessage>;
                            if (list == null) continue;
                            var mine = list.FirstOrDefault(m => !m.IsIncoming && !string.IsNullOrEmpty(m.Id) && m.Id == receiptId);
                            if (mine != null)
                            {
                                mine.Status = MessageStatus.Read;
                                HistoryManager.SaveHistory(tab.Jid, list);
                                break;
                            }
                        }
                    }
                    return;
                }

                // 2) обычное сообщение, возможно с префиксом id ( ##id:{id}##text )
                string incomingId = null;
                string realText = body;

                if (body.StartsWith(MetaPrefixId))
                {
                    int start = MetaPrefixId.Length;
                    int end = body.IndexOf("##", start);
                    if (end > start)
                    {
                        incomingId = body.Substring(start, end - start);
                        realText = body.Substring(end + 2);
                    }
                }

                var chatTab = ChatTabsItems.FirstOrDefault(t => t.Jid == bareJid);
                if (chatTab == null)
                {
                    var messages = HistoryManager.LoadHistory(bareJid);
                    var incoming = new ChatMessage
                    {
                        Id = incomingId,
                        Author = bareJid,
                        Text = realText,
                        Time = DateTime.Now,
                        IsIncoming = true,
                        Status = MessageStatus.Sent // непрочитано
                    };
                    messages.Add(incoming);
                    chatTab = new ChatTab { Jid = bareJid, Header = bareJid, Content = messages };
                    ChatTabsItems.Add(chatTab);
                    HistoryManager.SaveHistory(bareJid, messages);
                }
                else
                {
                    var existing = chatTab.Content as ObservableCollection<ChatMessage>;
                    if (existing != null)
                    {
                        var incoming = new ChatMessage
                        {
                            Id = incomingId,
                            Author = bareJid,
                            Text = realText,
                            Time = DateTime.Now,
                            IsIncoming = true,
                            Status = MessageStatus.Sent
                        };
                        existing.Add(incoming);
                        HistoryManager.SaveHistory(bareJid, existing);
                    }
                }
            });
        }

        // ==== отправка сообщений: добавляем мета-ид в тело (##id:GUID##text) ====
        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (_client == null || !_client.Connected)
            {
                MessageBox.Show("Клиент не подключён к серверу.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var chatTab = ChatTabs.SelectedItem as ChatTab;
            var messages = chatTab != null ? chatTab.Content as ObservableCollection<ChatMessage> : null;

            if (chatTab != null && messages != null)
            {
                if (!string.IsNullOrWhiteSpace(MessageTextBox.Text))
                {
                    try
                    {
                        string msgId = Guid.NewGuid().ToString("N");
                        string payload = $"{MetaPrefixId}{msgId}##{MessageTextBox.Text}";

                        // отправляем текст с префиксом
                        _client.SendMessage(new Jid(chatTab.Jid), payload);

                        // локально добавляем исходящее сообщение (Status = Sent)
                        var myMsg = new ChatMessage
                        {
                            Id = msgId,
                            Author = "Я",
                            Text = MessageTextBox.Text,
                            Time = DateTime.Now,
                            IsIncoming = false,
                            Status = MessageStatus.Sent
                        };
                        messages.Add(myMsg);
                        HistoryManager.SaveHistory(chatTab.Jid, messages);

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
                    var messages = HistoryManager.LoadHistory(contact.Jid);
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

        // ==== SelectionChanged: при открытии вкладки отправляем квитанции за входящие сообщения ====
        private void ChatTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChatTabs.SelectedItem is ChatTab selectedTab)
            {
                var list = selectedTab.Content as ObservableCollection<ChatMessage>;
                if (list == null) return;

                // выбираем непрочитанные входящие, которые имеют Id (иначе нельзя отправить receipt)
                var unreadIncoming = list.Where(m => m.IsIncoming && m.Status == MessageStatus.Sent && !string.IsNullOrEmpty(m.Id)).ToArray();
                if (unreadIncoming.Length == 0) return;

                // пометим локально как прочитанные
                foreach (var msg in unreadIncoming)
                    msg.Status = MessageStatus.Read;

                // сохраним историю
                HistoryManager.SaveHistory(selectedTab.Jid, list);

                // отправим каждому отправителю "квитанцию" в виде простого сообщения: ##receipt:{id}##
                foreach (var msg in unreadIncoming)
                {
                    try
                    {
                        string receiptPayload = $"{MetaPrefixReceipt}{msg.Id}##";
                        _client.SendMessage(new Jid(selectedTab.Jid), receiptPayload);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Ошибка при отправке receipt: " + ex.Message);
                    }
                }
            }
        }

        // Кнопка "История"
        private void OpenHistory_Click(object sender, RoutedEventArgs e)
        {
            if (ChatTabs.SelectedItem is ChatTab tab)
            {
                var h = new HistoryWindow(tab.Jid); // подгони под твой конструктор HistoryWindow
                h.Show();
            }
        }
    }

    // ChatTab модель (Content = ObservableCollection<ChatMessage>)
    public class ChatTab
    {
        public string Header { get; set; }
        public object Content { get; set; }
        public string Jid { get; set; }
    }

    // RelayCommand уже был у тебя; оставляем без изменений
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
