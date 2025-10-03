using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Documents; // Добавлен для RichTextBox и InlineUIContainer
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using S22.Xmpp;
using S22.Xmpp.Client;
using S22.Xmpp.Im;
using Newtonsoft.Json;
using System.Globalization;
using System.Windows.Controls.Primitives;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Text;





namespace CHATiCH
{
    public partial class ChatWindow : Window
    {
        private XmppClient _client;
        private DispatcherTimer _statusTimer;
        private DispatcherTimer _saveDebounceTimer;
        private DateTime _lastActivityTime;
        private bool _manualStatusSet = false;
        private string _uploadedFileName = null;


        public Uri BaseUri { get; } = new Uri(AppDomain.CurrentDomain.BaseDirectory);
        public ObservableCollection<UserContact> Contacts { get; set; } = new ObservableCollection<UserContact>();
        public ObservableCollection<ChatTab> ChatTabsItems { get; set; } = new ObservableCollection<ChatTab>();
        public ObservableCollection<ContactGroup> ContactGroups { get; set; } = new ObservableCollection<ContactGroup>();


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

            
            ChatTabs.ItemsSource = ChatTabsItems;

            CloseTabCommand = new RelayCommand<ChatTab>(CloseTab);
            emoji_btn.Click += EmojiBtn_Click;
            LoadRoster();
            file_btn.Click += FileBtn_Click;

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
                Icon = new System.Drawing.Icon("app.ico"), // Fix: полный путь для Icon
                ToolTipText = "CHATiCH",
                Visibility = Visibility.Visible
            };
            _trayIcon.TrayBalloonTipClicked += (s, e) => Activate();
        }
        private void EmojiBtn_Click(object sender, RoutedEventArgs e)
        {
            string emojiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Emoji");
            if (!Directory.Exists(emojiDir)) return;

            var files = Directory.GetFiles(emojiDir, "*.png");
            if (files.Length == 0) return;

            var menu = new ContextMenu
            {
                Background = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            var grid = new UniformGrid
            {
                Columns = 8,
            };

            foreach (var file in files)
            {
                var img = new Image
                {
                    Source = new BitmapImage(new Uri(file)),
                    Width = 25,
                    Height = 25,
                    Margin = new Thickness(2),
                    Cursor = Cursors.Hand // курсор “палец”
                };

                img.MouseLeftButtonDown += (s, ev) =>
                {
                    var emojiImage = new Image
                    {
                        Source = new BitmapImage(new Uri(file)),
                        Width = 20, // размер в чате
                        Height = 20,
                        Tag = Path.GetFileName(file)
                    };
                    var container = new InlineUIContainer(emojiImage, MessageRichBox.CaretPosition);
                    MessageRichBox.CaretPosition = container.ElementEnd;
                    MessageRichBox.Focus();
                    menu.IsOpen = false;
                };

                grid.Children.Add(img);
            }

            // помещаем сетку в один MenuItem без выделения
            var wrapperItem = new MenuItem
            {
                Header = grid,
                StaysOpenOnClick = true,
                Focusable = false,
                Background = Brushes.Transparent
            };

            menu.Items.Add(wrapperItem);

            menu.PlacementTarget = emoji_btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private async void FileBtn_Click(object sender, RoutedEventArgs e)
        {
            var chatTab = ChatTabs.SelectedItem as ChatTab;
            if (chatTab == null)
            {
                MessageBox.Show("Выберите чат перед отправкой файла!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            if (openFileDialog.ShowDialog() != true)
                return;

            string filePath = openFileDialog.FileName;

            try
            {
                FileUploadProgressBar.Visibility = Visibility.Visible;
                var progress = new Progress<double>(p => FileUploadProgressBar.Value = p);

                _uploadedFileName = await UploadFileAsync(filePath, progress);

                FileUploadProgressBar.Visibility = Visibility.Collapsed;
                SelectedFileText.Text = System.IO.Path.GetFileName(filePath);
            }
            catch (Exception ex)
            {
                FileUploadProgressBar.Visibility = Visibility.Collapsed;
                MessageBox.Show("Ошибка при загрузке файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async Task<string> UploadFileAsync(string filePath, IProgress<double> progress)
        {
            string serverUrl = "http://win-m4f2mfrj6i4.vkr.loc/fileschat/UploadFileHandler.ashx";

            using (var fs = File.OpenRead(filePath))
            using (var client = new System.Net.Http.HttpClient())
            {
                var totalBytes = fs.Length;
                var buffer = new byte[81920];
                int bytesRead;
                long uploaded = 0;
                var ms = new MemoryStream();

                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                    uploaded += bytesRead;
                    progress.Report(uploaded * 100.0 / totalBytes);
                }

                ms.Position = 0;

                using (var content = new System.Net.Http.MultipartFormDataContent())
                {
                    var streamContent = new System.Net.Http.StreamContent(ms);
                    content.Add(streamContent, "file", Path.GetFileName(filePath));

                    var response = await client.PostAsync(serverUrl, content);
                    response.EnsureSuccessStatusCode();

                    // просто читаем как строку
                    string uploadedFileName = await response.Content.ReadAsStringAsync();
                    return uploadedFileName.Trim(); // убираем возможные пробелы/переводы строки
                }
            }
        }


        private async void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                string url = e.Uri.AbsoluteUri;
                string fileName = System.IO.Path.GetFileName(url);

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    FileName = fileName,
                    Filter = "Все файлы|*.*"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var client = new WebClient())
                    {
                        await client.DownloadFileTaskAsync(new Uri(url), saveFileDialog.FileName);
                    }

                    MessageBox.Show($"Файл сохранён: {saveFileDialog.FileName}",
                                    "Скачивание завершено",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при скачивании файла: " + ex.Message,
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }

            e.Handled = true;
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
                var groupsDict = new Dictionary<string, ContactGroup>();

                foreach (var item in roster)
                {
                    // берем только группы, начинающиеся с "IM_"
                    var imGroup = item.Groups.FirstOrDefault(g => g.StartsWith("IM_"));
                    if (imGroup == null) continue; // пропускаем пользователя без IM_ группы

                    string groupName = imGroup.Substring(3); // убираем префикс IM_

                    if (!groupsDict.ContainsKey(groupName))
                        groupsDict[groupName] = new ContactGroup { Name = groupName };

                    string bareJid = item.Jid.Node + "@" + item.Jid.Domain;

                    if (!groupsDict[groupName].Contacts.Any(c => c.Jid == bareJid))
                    {
                        var contact = new UserContact
                        {
                            Jid = bareJid,
                            Name = string.IsNullOrEmpty(item.Name) ? bareJid : item.Name,
                            Availability = Availability.Offline,
                            StatusText = "Неизвестен"
                        };
                        groupsDict[groupName].Contacts.Add(contact);
                    }
                }

                // Обновляем ObservableCollection для UI
                Dispatcher.Invoke(() =>
                {
                    ContactGroups.Clear();
                    foreach (var group in groupsDict.Values.OrderBy(g => g.Name))
                        ContactGroups.Add(group);
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
                string bareJid = e.Jid.Node + "@" + e.Jid.Domain;

                foreach (var group in ContactGroups)
                {
                    var contact = group.Contacts.FirstOrDefault(c => c.Jid == bareJid);
                    if (contact != null)
                    {
                        contact.Availability = e.Status.Availability;
                        contact.StatusText = e.Status.Message ?? "Неизвестен";
                        break;
                    }
                }
            });
        }


        private void OnRosterUpdated(object sender, EventArgs e) => Dispatcher.Invoke(LoadRoster);

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string body = e.Message?.Body ?? string.Empty;
                string bareJid = e.Jid.Node + "@" + e.Jid.Domain;

                if (string.IsNullOrWhiteSpace(body))
                    return;

                // Убираем MetaEscape в начале
                if (body.StartsWith(MetaEscape))
                    body = body.Substring(MetaEscape.Length);

                // --- Receipt (подтверждение доставки/прочтения) ---
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

                // --- Состояния (печатает/активен/пауза) ---
                if (body.StartsWith(MetaPrefixState))
                {
                    int start = MetaPrefixState.Length;
                    int end = body.IndexOf("##", start);
                    string state = end > start ? body.Substring(start, end - start) : "";

                    var chatTab = ChatTabsItems.FirstOrDefault(t => t.Jid == bareJid);
                    if (chatTab != null)
                    {
                        chatTab.TypingText = state == "composing" ? $"{bareJid} печатает..." : "";
                    }
                    return;
                }

                // --- Обычные сообщения / файлы / эмодзи ---
                string incomingId = null;
                string realText = body;

                // Если есть Id
                if (body.StartsWith(MetaPrefixId))
                {
                    int start = MetaPrefixId.Length;
                    int end = body.IndexOf("##", start);
                    if (end > start)
                    {
                        incomingId = body.Substring(start, end - start);
                        realText = body.Substring(end + 2);

                        // 🔥 убираем escape, если он есть
                        if (realText.StartsWith(MetaEscape))
                            realText = realText.Substring(MetaEscape.Length);
                    }
                }
                // ... (существующий код парсинга ID)

                if (realText.StartsWith(MetaEscape))
                {
                    realText = realText.Substring(MetaEscape.Length);
                }

                // ... (существующий код парсинга ID и удаления MetaEscape)

                var match = Regex.Match(realText, @"^\[(.+?)\]\((https?://.+?)\)$");
                if (match.Success)
                {
                    ChatMessage incoming;
                    string fileName = match.Groups[1].Value;
                    string fileUrl = match.Groups[2].Value;

                    incoming = new ChatMessage
                    {
                        Id = incomingId,
                        Author = bareJid,
                        Text = realText,  // Оставьте realText для fallback-рендеринга в MarkdownTemplate
                        Time = DateTime.Now,
                        IsIncoming = true,
                        Status = MessageStatus.Sent,
                        FileName = fileName,
                        FileUrl = fileUrl
                    };
                }
                else
                {
                    ChatMessage incoming;
                    incoming = new ChatMessage
                    {
                        Id = incomingId,
                        Author = bareJid,
                        Text = realText,
                        Time = DateTime.Now,
                        IsIncoming = true,
                        Status = MessageStatus.Sent
                    };
                }

                // ... (остальное без изменений)

                // Загружаем/создаём вкладку для контакта
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
                    string fileName = null;
                    string fileUrl = null;

                    // Проверяем Markdown-ссылку, НО не для эмодзи (![](...))
                    var mdLinkMatch = System.Text.RegularExpressions.Regex.Match(realText, @"(?<!!)\[(.*?)\]\((.*?)\)");
                    if (mdLinkMatch.Success)
                    {
                        fileName = mdLinkMatch.Groups[1].Value;
                        fileUrl = mdLinkMatch.Groups[2].Value;
                    }

                    var incoming = new ChatMessage
                    {
                        Id = incomingId,
                        Author = bareJid,
                        Text = realText,          // В JSON всё равно будет полный текст (с эмодзи или Markdown)
                        Time = DateTime.Now,
                        IsIncoming = true,
                        Status = MessageStatus.Sent,
                        FileName = fileName,      // Только если это файл
                        FileUrl = fileUrl
                    };
                    messages.Add(incoming);
                    ScheduleSave(bareJid, messages);

                    // Уведомление в трее, если окно неактивно
                    if (!IsActive)
                    {
                        _trayIcon.ShowBalloonTip("Новое сообщение",
                            $"{bareJid}: {realText.Truncate(50)}",
                            BalloonIcon.Info);
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
            string markdownText = GetMarkdownFromRichTextBox();

            if (!string.IsNullOrEmpty(_uploadedFileName))
            {
                // Очищаем имя файла от лишних скобок и пробелов
                string cleanedFileName = Path.GetFileName(SelectedFileText.Text.TrimEnd(')', ' '));
                string cleanedFileUrl = "http://win-m4f2mfrj6i4.vkr.loc/fileschat/files/" + _uploadedFileName.TrimEnd(')', ' ');

                var fileMessage = new ChatMessage
                {
                    Author = "Я",
                    Time = DateTime.Now,
                    IsIncoming = false,
                    Status = MessageStatus.Sent,
                    Text = $"[{cleanedFileName}]({cleanedFileUrl})", // Markdown для отображения
                    FileName = cleanedFileName,
                    FileUrl = cleanedFileUrl
                };

                messages.Add(fileMessage);

                // Отправка через XMPP
                string msgId = Guid.NewGuid().ToString("N");
                string payload = MetaPrefixId + msgId + "##" + (fileMessage.Text.StartsWith("##") ? MetaEscape + fileMessage.Text : fileMessage.Text);                
                _client.SendMessage(new Jid(chatTab.Jid), payload);

                SelectedFileText.Text = "";
                _uploadedFileName = null;
            }






            if (messages != null && !string.IsNullOrWhiteSpace(markdownText))
            {
                try
                {
                    string msgId = Guid.NewGuid().ToString("N");
                    string payload = MetaPrefixId + msgId + "##" + (markdownText.StartsWith("##") ? MetaEscape + markdownText : markdownText);

                    _client.SendMessage(new Jid(chatTab.Jid), payload);

                    var myMsg = new ChatMessage
                    {
                        Id = msgId,
                        Author = "Я",
                        Text = markdownText,
                        Time = DateTime.Now,
                        IsIncoming = false,
                        Status = MessageStatus.Sent
                    };
                    Console.WriteLine(markdownText);
                    messages.Add(myMsg);
                    ScheduleSave(chatTab.Jid, messages);

                    MessageRichBox.Document.Blocks.Clear();
                    SendChatState(chatTab.Jid, "active");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка при отправке: " + ex.Message);
                }
            }
        }
        private string GetMarkdownFromRichTextBox()
        {
            var markdown = new System.Text.StringBuilder();
            foreach (var block in MessageRichBox.Document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (var inline in paragraph.Inlines)
                    {
                        if (inline is InlineUIContainer container && container.Child is Image image)
                        {
                            var filename = image.Tag as string;
                            if (filename != null)
                            {
                                // вставляем эмодзи как тег <img> с фиксированным размером
                                markdown.Append($"![](Emoji/{filename})");
                            }
                        }
                        else if (inline is Run run)
                        {
                            markdown.Append(run.Text);
                        }
                    }
                }
            }
            return markdown.ToString().Trim();
        }


        private void MessageRichBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                SendMessage_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void MessageRichBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var chatTab = ChatTabs.SelectedItem as ChatTab;
            if (chatTab != null)
            {
                var jid = chatTab.Jid;
                var text = new TextRange(MessageRichBox.Document.ContentStart, MessageRichBox.Document.ContentEnd).Text.Trim();

                if (!string.IsNullOrEmpty(text))
                {
                    // сразу отправляем "печатает..."
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

        private void ContactsTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Получаем элемент под мышью
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is UserContact contact)
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
                    var unreadIncoming = list
                        .Where(m => m.IsIncoming && m.Status == MessageStatus.Sent && !string.IsNullOrEmpty(m.Id))
                        .ToArray();

                    if (unreadIncoming.Length > 0)
                    {
                        // Автоматически помечаем как прочитанное, если окно активно
                        if (this.IsActive)
                        {
                            foreach (var msg in unreadIncoming)
                                msg.Status = MessageStatus.Read;

                            ScheduleSave(selectedTab.Jid, list);

                            // Отправляем receipt отправителю
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
        }

        public class ContactGroup : INotifyPropertyChanged
        {
            public string Name { get; set; }
            public ObservableCollection<UserContact> Contacts { get; set; } = new ObservableCollection<UserContact>();

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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

        private void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void MarkdownScrollViewer_Scroll(object sender, ScrollEventArgs e)
        {

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
    public class RelativePathToUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new Uri(AppDomain.CurrentDomain.BaseDirectory); // Base dir for Emoji/
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
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