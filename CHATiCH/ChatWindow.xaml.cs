using S22.Xmpp;
using S22.Xmpp.Client;
using S22.Xmpp.Im;
using System;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Newtonsoft.Json;

namespace CHATiCH
{
    public partial class ChatWindow : Window
    {
        private XmppClient _client;
        private DispatcherTimer _statusTimer;
        private DispatcherTimer _saveDebounceTimer;
        private DateTime _lastActivityTime;
        private bool _manualStatusSet = false;

        public ObservableCollection<UserContact> Contacts { get; set; } = new ObservableCollection<UserContact>();
        public ObservableCollection<ChatTab> ChatTabsItems { get; set; } = new ObservableCollection<ChatTab>();

        public ICommand CloseTabCommand { get; }

        private const string MetaPrefixId = "##id:";
        private const string MetaPrefixReceipt = "##receipt:";
        private const string MetaPrefixState = "##state:";
        private const string MetaEscape = "##escape##";

        private TaskbarIcon _trayIcon;

        private DispatcherTimer _typingTimer;

        private string _debounceJid;
        private ObservableCollection<ChatMessage> _debounceMessages;

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

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();

            _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _saveDebounceTimer.Tick += SaveDebounceTimer_Tick;

            InputManager.Current.PreProcessInput += OnActivity;
            Closing += ChatWindow_Closing;

            if (StatusComboBox != null && StatusComboBox.Items.Count > 0)
                StatusComboBox.SelectedIndex = 0;

            UpdateStatus(Availability.Online, StatusMessageBox?.Text ?? "Online via WPF");

            HistoryManager.CleanupOldHistory();

            _trayIcon = new TaskbarIcon
            {
                Icon = new Icon("app.ico"),
                ToolTipText = "CHATiCH",
                Visibility = Visibility.Visible
            };
            _trayIcon.TrayBalloonTipClicked += (s, e) => Activate();
        }

        private void ChatWindow_Closing(object sender, CancelEventArgs e)
        {
            _trayIcon?.Dispose();
            try
            {
                if (_client != null && _client.Connected)
                    _client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при отключении: {ex.Message}");
            }
        }

        private void OnActivity(object sender, PreProcessInputEventArgs e)
        {
            _lastActivityTime = DateTime.Now;
            if (!_manualStatusSet && StatusComboBox != null)
            {
                var selected = StatusComboBox.SelectedItem as ComboBoxItem;
                if (selected?.Tag?.ToString() == "Away")
                {
                    StatusComboBox.SelectedIndex = 0;
                    UpdateStatus(Availability.Online, StatusMessageBox?.Text ?? "Online via WPF");
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
                        if (StatusComboBox?.SelectedIndex != 1)
                        {
                            StatusComboBox.SelectedIndex = 1;
                            UpdateStatus(Availability.Away, StatusMessageBox?.Text ?? "Отошел");
                        }
                    }
                    else
                    {
                        var sel = StatusComboBox?.SelectedItem as ComboBoxItem;
                        if (sel?.Tag?.ToString() == "Away")
                        {
                            StatusComboBox.SelectedIndex = 0;
                            UpdateStatus(Availability.Online, StatusMessageBox?.Text ?? "Online via WPF");
                        }
                    }
                }

                var currentAvailability = GetUiSelectedAvailability();
                var currentStatusText = StatusMessageBox?.Text ?? "";
                UpdateStatus(currentAvailability, currentStatusText);

                var roster = _client.GetRoster();
                Dispatcher.Invoke(() =>
                {
                    foreach (var item in roster)
                    {
                        var bareJid = item.Jid.Node + "@" + item.Jid.Domain; // Fix

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
            var selected = StatusComboBox?.SelectedItem as ComboBoxItem;
            var tag = selected?.Tag as string;
            return string.Equals(tag, "Away", StringComparison.OrdinalIgnoreCase) ? Availability.Away : Availability.Online;
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
                        var bareJid = item.Jid.Node + "@" + item.Jid.Domain; // Fix

                        if (!Contacts.Any(c => c.Jid == bareJid))
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

        private void OnStatusChanged(object sender, StatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var bareJid = e.Jid.Node + "@" + e.Jid.Domain; // Fix
                var contact = Contacts.FirstOrDefault(c => c.Jid == bareJid);
                if (contact != null)
                {
                    contact.Availability = e.Status.Availability;
                    contact.StatusText = e.Status.Message ?? "Неизвестен";
                }
            });
        }

        private void OnRosterUpdated(object sender, EventArgs e) => Dispatcher.Invoke(LoadRoster);

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string body = e.Message?.Body ?? string.Empty;
                string bareJid = e.Jid.Node + "@" + e.Jid.Domain; // Fix

                if (string.IsNullOrWhiteSpace(body))
                    return;

                if (body.StartsWith(MetaEscape))
                    body = body.Substring(MetaEscape.Length);

                if (body.StartsWith(MetaPrefixReceipt))
                {
                    int start = MetaPrefixReceipt.Length;
                    int end = body.IndexOf("##", start);
                    if (end > start)
                    {
                        string receiptId = body.Substring(start, end - start);
                        foreach (var tab in ChatTabsItems)
                        {
                            if (tab.Content is ObservableCollection<ChatMessage> list)
                            {
                                var mine = list.FirstOrDefault(m => !m.IsIncoming && m.Id == receiptId);
                                if (mine != null)
                                {
                                    mine.Status = MessageStatus.Read;
                                    ScheduleSave(tab.Jid, list);
                                    break;
                                }
                            }
                        }
                    }
                    return;
                }

                if (body.StartsWith("##state:"))
                {
                    int start = "##state:".Length;
                    int end = body.IndexOf("##", start);
                    string state = end > start ? body.Substring(start, end - start) : "";

                    var chatTab = ChatTabsItems.FirstOrDefault(t => t.Jid == bareJid);
                    if (chatTab != null)
                    {
                        if (state == "composing")
                            chatTab.TypingText = $"{bareJid} печатает...";
                        else if (state == "paused" || state == "active")
                            chatTab.TypingText = "";
                        else
                            chatTab.TypingText = "";
                    }

                    return;
                }

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

                var chatTabExist = ChatTabsItems.FirstOrDefault(t => t.Jid == bareJid);
                ObservableCollection<ChatMessage> messages;
                if (chatTabExist == null)
                {
                    messages = HistoryManager.LoadHistory(bareJid);
                    chatTabExist = new ChatTab { Jid = bareJid, Header = bareJid, Content = messages, TypingText = "" };
                    ChatTabsItems.Add(chatTabExist);
                }
                else
                {
                    messages = chatTabExist.Content as ObservableCollection<ChatMessage>;
                }

                if (messages != null)
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
                    messages.Add(incoming);
                    ScheduleSave(bareJid, messages);

                    if (!IsActive)
                    {
                        _trayIcon.ShowBalloonTip("Новое сообщение", $"{bareJid}: {realText.Truncate(50)}", BalloonIcon.Info);
                    }
                }
            });
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (_client == null || !_client.Connected) return;

            var chatTab = ChatTabs.SelectedItem as ChatTab;
            if (chatTab == null)
            {
                MessageBox.Show("Выберите чат перед отправкой сообщения!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var messages = chatTab.Content as ObservableCollection<ChatMessage>;
            if (messages != null && !string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                try
                {
                    string text = MessageTextBox.Text;
                    string msgId = Guid.NewGuid().ToString("N");
                    string payload = MetaPrefixId + msgId + "##" + (text.StartsWith("##") ? MetaEscape + text : text);

                    _client.SendMessage(new Jid(chatTab.Jid), payload);

                    var myMsg = new ChatMessage
                    {
                        Id = msgId,
                        Author = "Я",
                        Text = text,
                        Time = DateTime.Now,
                        IsIncoming = false,
                        Status = MessageStatus.Sent
                    };
                    messages.Add(myMsg);
                    ScheduleSave(chatTab.Jid, messages);

                    MessageTextBox.Clear();
                    SendChatState(chatTab.Jid, "active");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка при отправке: " + ex.Message);
                }
            }
        }

        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SendMessage_Click(sender, new RoutedEventArgs());
        }

        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var chatTab = ChatTabs.SelectedItem as ChatTab;
            if (chatTab != null)
            {
                var jid = chatTab.Jid;
                if (!string.IsNullOrEmpty(MessageTextBox.Text))
                {
                    SendChatState(jid, "composing");

                    if (_typingTimer == null)
                    {
                        _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                        _typingTimer.Tick += (s, args) =>
                        {
                            _typingTimer.Stop();
                            SendChatState(jid, "paused");
                        };
                    }
                    else
                    {
                        _typingTimer.Stop();
                    }

                    _typingTimer.Start();
                }
                else
                {
                    SendChatState(jid, "paused");
                }
            }
        }

        private void SendChatState(string jid, string state)
        {
            string payload = "##state:" + state + "##";
            _client.SendMessage(new Jid(jid), payload);
        }

        private void ContactsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ContactsList.SelectedItem is UserContact contact)
            {
                var existingTab = ChatTabsItems.FirstOrDefault(t => t.Jid == contact.Jid);
                if (existingTab == null)
                {
                    var messages = HistoryManager.LoadHistory(contact.Jid);
                    var newTab = new ChatTab { Jid = contact.Jid, Header = contact.Name, Content = messages, TypingText = "" };
                    ChatTabsItems.Add(newTab);
                    ChatTabs.SelectedItem = newTab;
                }
                else
                {
                    ChatTabs.SelectedItem = existingTab;
                }
            }
        }

        private string GetBareJid(Jid jid) => jid.Node + "@" + jid.Domain; // Fix

        private void CloseTab(ChatTab tab)
        {
            if (tab != null && ChatTabsItems.Contains(tab))
                ChatTabsItems.Remove(tab);
        }

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _manualStatusSet = true;
            var availability = GetUiSelectedAvailability();
            UpdateStatus(availability, StatusMessageBox?.Text ?? "");
        }

        private void StatusMessageBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var availability = GetUiSelectedAvailability();
            UpdateStatus(availability, StatusMessageBox?.Text ?? "");
        }

        private void UpdateStatus(Availability availability, string statusText)
        {
            try
            {
                if (_client != null && _client.Connected)
                    _client.SetStatus(availability, statusText);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка обновления статуса: " + ex.Message);
            }
        }

        private void ChatTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChatTabs.SelectedItem is ChatTab selectedTab)
            {
                if (selectedTab.Content is ObservableCollection<ChatMessage> list)
                {
                    var unreadIncoming = list.Where(m => m.IsIncoming && m.Status == MessageStatus.Sent && !string.IsNullOrEmpty(m.Id)).ToArray();
                    if (unreadIncoming.Length > 0)
                    {
                        foreach (var msg in unreadIncoming)
                            msg.Status = MessageStatus.Read;

                        ScheduleSave(selectedTab.Jid, list);

                        foreach (var msg in unreadIncoming)
                        {
                            try
                            {
                                string receiptPayload = MetaPrefixReceipt + msg.Id + "##";
                                _client.SendMessage(new Jid(selectedTab.Jid), receiptPayload);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Ошибка отправки receipt: " + ex.Message);
                            }
                        }
                    }
                }
            }
        }

        private void OpenHistory_Click(object sender, RoutedEventArgs e)
        {
            if (ChatTabs.SelectedItem is ChatTab tab)
            {
                new HistoryWindow(tab.Jid).Show();
            }
        }

        private void ScheduleSave(string jid, ObservableCollection<ChatMessage> messages)
        {
            if (messages == null || string.IsNullOrEmpty(jid)) return;
            _debounceJid = jid;
            _debounceMessages = messages;
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        private void SaveDebounceTimer_Tick(object sender, EventArgs e)
        {
            _saveDebounceTimer.Stop();
            if (_debounceMessages != null && !string.IsNullOrEmpty(_debounceJid))
            {
                try
                {
                    HistoryManager.SaveHistory(_debounceJid, _debounceMessages);
                    Console.WriteLine($"История сохранена: {_debounceJid}, сообщений: {_debounceMessages.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка сохранения истории: {ex}");
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(Contacts);
            view.Filter = o =>
            {
                if (o is UserContact contact)
                {
                    string search = SearchTextBox.Text.ToLower();
                    return string.IsNullOrEmpty(search) ||
                           contact.Name.ToLower().Contains(search) ||
                           contact.Jid.ToLower().Contains(search);
                }
                return false;
            };
        }
    }

    public class ChatTab : INotifyPropertyChanged
    {
        public string Header { get; set; }
        public object Content { get; set; }
        public string Jid { get; set; }

        private string _typingText = "";
        public string TypingText
        {
            get { return _typingText; }
            set
            {
                if (_typingText != value)
                {
                    _typingText = value;
                    OnPropertyChanged(nameof(TypingText));
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

    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }
    }
}