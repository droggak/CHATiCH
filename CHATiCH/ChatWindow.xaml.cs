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
using System.Windows.Documents;
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
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

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
        private readonly string favoritesFile;
        private string _selectedFilePath;
        private int _dragDepth = 0;
        private bool _isDraggingFile = false;
        private bool _isAutomaticChange = false;
        public Uri BaseUri { get; } = new Uri(AppDomain.CurrentDomain.BaseDirectory);
        public ObservableCollection<UserContact> Contacts { get; set; } = new ObservableCollection<UserContact>();
        public ObservableCollection<ChatTab> ChatTabsItems { get; set; } = new ObservableCollection<ChatTab>();
        public ObservableCollection<ContactGroup> ContactGroups { get; set; } = new ObservableCollection<ContactGroup>();
        private string StatusFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"ChatStatus.txt");

        public ObservableCollection<UserContact> FavoriteContacts { get; set; } = new ObservableCollection<UserContact>();
        public ObservableCollection<string> Reactions { get; set; } = new ObservableCollection<string>();


        public ICommand CloseTabCommand { get; }

        private const string MetaPrefixId = "##id:";
        private const string MetaPrefixReceipt = "##receipt:";
        private const string MetaPrefixState = "##state:";
        private const string MetaEscape = "##escape##";
        private DateTime _lastMouseMoveTime = DateTime.MinValue;
        private TaskbarIcon _trayIcon;

        private DispatcherTimer _typingTimer;

        private string _debounceJid;
        private ObservableCollection<ChatMessage> _debounceMessages;
        private ChatMessage _editingMessage = null;

        public ChatWindow(XmppClient client)
        {
            InitializeComponent();
            this.Loaded += ChatWindow_Loaded;
            this.KeyDown += OnUserActivity;      // Добавлено: клавиатура
            this.MouseMove += OnUserActivity;    // Добавлено: движение мыши
            this.MouseDown += OnUserActivity;    // Добавлено: клик мыши

            _client = client;
            DataContext = this;
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appFolder = Path.Combine(documentsPath, "CHATiCH");
            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            favoritesFile = Path.Combine(appFolder, "favorites.json");
            // Загружаем сохранённый статус
            StatusMessageBox.Text = Properties.Settings.Default.UserStatus;

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
                Icon = new System.Drawing.Icon("F:\\VKR\\CHATiCH\\CHATiCH\\bin\\Debug\\app.ico"), // Fix: полный путь для Icon
                ToolTipText = "CHATiCH",
                Visibility = Visibility.Visible
            };
            _trayIcon.TrayBalloonTipClicked += (s, e) => Activate();
        }
        private void OnUserActivity(object sender, RoutedEventArgs e)
        {
            _lastActivityTime = DateTime.Now;
            CheckAndResetStatus();  // Немедленно проверяем и сбрасываем статус, если нужно

            // Опционально: debounce для MouseMove (если нужно, чтобы не лагало от частых вызовов)
            if (e is MouseEventArgs)
            {
                if ((DateTime.Now - _lastMouseMoveTime).TotalSeconds < 1)
                    return;  // Игнор если <1 сек
                _lastMouseMoveTime = DateTime.Now;
            }
        }
        private void CheckAndResetStatus()
        {
            if (!_manualStatusSet && StatusComboBox != null)
            {
                var selected = StatusComboBox.SelectedItem as ComboBoxItem;
                if (selected?.Tag?.ToString() == "Away")
                {
                    _isAutomaticChange = true;  // Устанавливаем флаг перед изменением
                    StatusComboBox.SelectedIndex = 0;
                    UpdateStatus(Availability.Online, StatusMessageBox?.Text ?? "Online via WPF");

                    // Обновляем свой шарик статуса, если selfContact существует
                    string selfJid = _client.Jid.Node + "@" + _client.Jid.Domain;
                    var selfContact = Contacts.FirstOrDefault(c => c.Jid == selfJid);
                    if (selfContact != null)
                    {
                        selfContact.Availability = Availability.Online;
                    }
                }
            }
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
        public class EmojiToImageConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is string emojiFile)
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Emoji", emojiFile);
                    if (File.Exists(path))
                        return new BitmapImage(new Uri(path));
                }
                return null;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            if (!(btn.DataContext is ChatMessage message)) return;

            string emojiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmojiReactions");
            if (!Directory.Exists(emojiDir)) return;

            var files = Directory.GetFiles(emojiDir, "*.png");
            if (files.Length == 0) return;

            var menu = new ContextMenu();
            var grid = new UniformGrid { Columns = 5 };

            foreach (var file in files)
            {
                var img = new Image
                {
                    Source = new BitmapImage(new Uri(file)),
                    Width = 25,
                    Height = 25,
                    Margin = new Thickness(2),
                    Cursor = Cursors.Hand,
                    Tag = file
                };

                img.MouseLeftButtonDown += (s, ev) =>
                {
                    if (!message.Reactions.Contains(file))
                    {
                        message.Reactions.Add(file);

                        // Отправляем реакцию собеседнику
                        var tab = ChatTabs.SelectedItem as ChatTab;
                        if (tab != null)
                        {
                            string payload = $"##react:{message.Id}##" + Path.GetFileName(file);
                            _client.SendMessage(new Jid(tab.Jid), payload);
                        }

                        // Сохраняем историю
                        if (tab?.Content is ObservableCollection<ChatMessage> list)
                            ScheduleSave(tab.Jid, list);
                    }

                    menu.IsOpen = false;
                };

                grid.Children.Add(img);
            }

            menu.Items.Add(new MenuItem { Header = grid, StaysOpenOnClick = true, Focusable = false, Background = Brushes.Transparent });
            menu.PlacementTarget = btn;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }



        private void ReactionImage_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image img && img.DataContext is string emoji)
            {
                // Получаем родительский ChatMessage
                if (img.TemplatedParent is ContentPresenter cp && cp.DataContext is ChatMessage message)
                {
                    message.Reactions.Remove(emoji);

                    // Сохраняем изменения
                    var tab = ChatTabs.SelectedItem as ChatTab;
                    if (tab?.Content is ObservableCollection<ChatMessage> list)
                        ScheduleSave(tab.Jid, list);
                }
            }
        }

        private void ReactionImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is Image img && img.DataContext is string reactionPath)
            {
                // Находим сообщение, которому принадлежит эта реакция
                if (FindParent<ListBoxItem>(img) is ListBoxItem item && item.DataContext is ChatMessage message)
                {
                    message.RemoveReaction(reactionPath);

                    // сохраняем историю
                    var tab = ChatTabs.SelectedItem as ChatTab;
                    if (tab?.Content is ObservableCollection<ChatMessage> list)
                        ScheduleSave(tab.Jid, list);
                }
            }
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        private void FileBtn_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите файл для отправки",
                Filter = "Все файлы (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                var fileInfo = new FileInfo(filePath);

                var chatTab = ChatTabs.SelectedItem as ChatTab;
                if (chatTab == null)
                {
                    MessageBox.Show("Выберите чат перед прикреплением файла!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Отображаем карточку предпросмотра файла
                FilePreviewPanel.Visibility = Visibility.Visible;
                FileUploadProgressBar.Visibility = Visibility.Collapsed;
                FileUploadProgressBar.Value = 0;

                SelectedFileName.Text = fileInfo.Name;
                SelectedFileSize.Text = $"{fileInfo.Length / 1024.0 / 1024.0:F2} MB";
                _selectedFilePath = filePath;
                _uploadedFileName = null;
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
            // Сохраняем текст из StatusMessageBox в файл
            try
            {
                if (StatusMessageBox != null)
                {
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "ChatStatus.txt");

                    File.WriteAllText(path, StatusMessageBox.Text, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка сохранения статуса при закрытии: " + ex.Message);
            }

            // Очистка ресурсов
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
            CheckAndResetStatus();  // Добавлено: вызов сброса
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                string selfJid = _client.Jid.Node + "@" + _client.Jid.Domain; // собственный JID
                var selfContact = Contacts.FirstOrDefault(c => c.Jid == selfJid);

                if (!_manualStatusSet)
                {
                    var idleTime = DateTime.Now - _lastActivityTime;
                    if (idleTime.TotalMinutes >= 5)
                    {
                        if (StatusComboBox?.SelectedIndex != 1)
                        {
                            _isAutomaticChange = true;  // Устанавливаем флаг перед изменением
                            StatusComboBox.SelectedIndex = 1;
                            UpdateStatus(Availability.Away, StatusMessageBox?.Text ?? "Отошел");
                            if (selfContact != null)
                                selfContact.Availability = Availability.Away;
                        }
                    }
                    else
                    {
                        CheckAndResetStatus();
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
                        var bareJid = item.Jid.Node + "@" + item.Jid.Domain;

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
                
                // Папка для конфигурации пользователя
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CHATiCH");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string favPath = Path.Combine(folder, "favorites.json");

                // Загружаем избранные из файла
                var favoriteJids = new List<string>();
                if (File.Exists(favPath))
                {
                    favoriteJids = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(favPath));
                }

                // Очищаем коллекцию избранного перед добавлением
                FavoriteContacts.Clear();

                foreach (var item in roster)
                {
                    // берем только группы, начинающиеся с "IM_"
                    var imGroup = item.Groups.FirstOrDefault(g => g.StartsWith("IM_"));
                    if (imGroup == null) continue;
                    Console.WriteLine(imGroup);
                    string groupName = imGroup.Substring(3);

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
                            StatusText = "Неизвестен",
                            IsFavorite = favoriteJids.Contains(bareJid) // помечаем как избранное
                        };
                        contact.PropertyChanged += UserContact_PropertyChanged;

                        groupsDict[groupName].Contacts.Add(contact);

                        // Если контакт избран — добавляем в коллекцию избранного
                        if (contact.IsFavorite)
                            FavoriteContacts.Add(contact);
                    }
                }

                // Обновляем ObservableCollection для UI
                Dispatcher.Invoke(() =>
                {
                    ContactGroups.Clear();

                    // 1. Добавляем "Избранное" как отдельную группу
                    var favoriteGroup = new ContactGroup
                    {
                        Name = "Избранное",
                        Contacts = FavoriteContacts
                    };
                    ContactGroups.Add(favoriteGroup);

                    // 2. Добавляем остальные группы
                    foreach (var group in groupsDict.Values.OrderBy(g => g.Name))
                        ContactGroups.Add(group);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке ростера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GroupMessage_Click(object sender, RoutedEventArgs e)
        {
            var broadcastWindow = new SelectBroadcastWindow(ContactGroups, _client);
            broadcastWindow.Owner = this;
            broadcastWindow.ShowDialog();
        }

        private void ForwardMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ChatMessage message)
            {
                // Диалог выбора контакта
                var selectWindow = new SelectContactWindow(ContactGroups);
                if (selectWindow.ShowDialog() == true)
                {
                    var targetJid = selectWindow.SelectedJid;
                    if (string.IsNullOrEmpty(targetJid))
                        return;

                    // Находим или создаём вкладку
                    var targetTab = ChatTabsItems.FirstOrDefault(t => t.Jid == targetJid);
                    if (targetTab == null)
                    {
                        var messages = HistoryManager.LoadHistory(targetJid);
                        targetTab = new ChatTab { Jid = targetJid, Header = targetJid, Content = messages };
                        ChatTabsItems.Add(targetTab);
                    }

                    var list = targetTab.Content as ObservableCollection<ChatMessage>;
                    string newId = Guid.NewGuid().ToString("N");

                    // Формируем пересланное сообщение
                    var newMessage = new ChatMessage
                    {
                        Id = newId,
                        Author = "Я",
                        Time = DateTime.Now,
                        IsIncoming = false,
                        Status = MessageStatus.Sent,
                        Text = message.Text,
                        FileName = message.FileName,
                        FileUrl = message.FileUrl,
                        ForwardedFrom = message.Author,
                        ForwardedText = message.Text
                    };

                    list.Add(newMessage);

                    // Отправляем XMPP-пакет с пометкой о пересылке
                    string payload = MetaPrefixId + newId + "##" + "##fwd:" + message.Author + "##" + message.Text;
                    _client.SendMessage(new Jid(targetJid), payload);

                    ScheduleSave(targetJid, list);
                    MessageBox.Show($"Переслано от {message.Author}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        private void SaveFavorites()
        {
            try
            {
                // Берём всех контактов, у которых IsFavorite = true
                var favoriteJids = ContactGroups
                    .SelectMany(g => g.Contacts)
                    .Where(c => c.IsFavorite)
                    .Select(c => c.Jid)
                    .ToList();

                // Сохраняем весь список в файл
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CHATiCH");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, "favorites.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(favoriteJids, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка сохранения избранного: " + ex.Message);
            }
        }


        private void UserContact_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserContact.IsFavorite))
            {
                var contact = sender as UserContact;
                if (contact == null) return;

                if (contact.IsFavorite)
                {
                    if (!FavoriteContacts.Contains(contact))
                        FavoriteContacts.Add(contact);
                }
                else
                {
                    if (FavoriteContacts.Contains(contact))
                        FavoriteContacts.Remove(contact);
                }

                SaveFavorites(); // Сохраняем весь список
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
                if (body.StartsWith("##react:"))
                {
                    int start = "##react:".Length;
                    int end = body.IndexOf("##", start);
                    if (end > start)
                    {
                        string reactId = body.Substring(start, end - start);
                        string reaction = body.Substring(end + 2);

                        foreach (var tab in ChatTabsItems)
                        {
                            if (tab.Content is ObservableCollection<ChatMessage> list)
                            {
                                var msg = list.FirstOrDefault(m => m.Id == reactId);
                                if (msg != null && !msg.Reactions.Contains(reaction))
                                {
                                    msg.Reactions.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmojiReactions", reaction));
                                }
                            }
                        }
                    }
                    return;
                }

                // Убираем MetaEscape в начале
                if (body.StartsWith(MetaEscape))
                    body = body.Substring(MetaEscape.Length);

                // --- Receipt ---
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

                // --- State (печатает/пауза) ---
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

                // --- Edit incoming message ---
                if (body.StartsWith("##edit:"))
                {
                    int start = "##edit:".Length;
                    int end = body.IndexOf("##", start);
                    if (end > start)
                    {
                        string editId = body.Substring(start, end - start);
                        string newText = body.Substring(end + 2);

                        foreach (var tab in ChatTabsItems)
                        {
                            if (tab.Content is ObservableCollection<ChatMessage> list)
                            {
                                var msg = list.FirstOrDefault(m => m.Id == editId);
                                if (msg != null && msg.Author != "Я")  // Только для чужих сообщений (не "Я")
                                {
                                    msg.Text = newText;
                                    msg.IsEdited = true;
                                    msg.OnPropertyChanged(nameof(ChatMessage.DisplayText));
                                    ScheduleSave(tab.Jid, list);
                                    break;
                                }
                            }
                        }
                    }
                    return;
                }
                // --- Reaction to message ---
                if (body.StartsWith("##react:"))
                {
                    int start = "##react:".Length;
                    int end = body.IndexOf("##", start);
                    if (end > start)
                    {
                        string reactId = body.Substring(start, end - start);
                        string reaction = body.Substring(end + 2);

                        foreach (var tab in ChatTabsItems)
                        {
                            if (tab.Content is ObservableCollection<ChatMessage> list)
                            {
                                var msg = list.FirstOrDefault(m => m.Id == reactId);
                                if (!msg.Reactions.Contains(reaction))
                                {
                                    msg.Reactions.Add(reaction);
                                }

                            }
                        }
                    }
                    return;
                }


                // --- Обычные сообщения / файлы / эмодзи ---
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

                        if (realText.StartsWith(MetaEscape))
                            realText = realText.Substring(MetaEscape.Length);
                    }
                }

                // Определяем Markdown-ссылку на файл
                string fileName = null;
                string fileUrl = null;
                var mdLinkMatch = Regex.Match(realText, @"(?<!!)\[(.*?)\]\((.*?)\)");
                if (mdLinkMatch.Success)
                {
                    fileName = mdLinkMatch.Groups[1].Value;
                    fileUrl = mdLinkMatch.Groups[2].Value;
                }

                var incoming = new ChatMessage
                {
                    Id = incomingId,
                    Author = bareJid,
                    Text = realText,
                    Time = DateTime.Now,
                    IsIncoming = true,
                    Status = MessageStatus.Sent,
                    FileName = fileName,
                    FileUrl = fileUrl
                };

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

                messages?.Add(incoming);

                // Если окно неактивно — увеличиваем счетчик непрочитанных
                if (!IsActive || ChatTabs.SelectedItem != chatTabExist)
                {
                    chatTabExist.UnreadCount++;
                    var contact = ContactGroups.SelectMany(g => g.Contacts)
                                               .FirstOrDefault(c => c.Jid == bareJid);
                    if (contact != null)
                        contact.UnreadCount++;
                }
                else
                {
                    foreach (var msg in messages.Where(m => m.IsIncoming))
                        msg.Status = MessageStatus.Read;
                    chatTabExist.UnreadCount = 0;

                    var contact = ContactGroups.SelectMany(g => g.Contacts)
                                               .FirstOrDefault(c => c.Jid == bareJid);
                    if (contact != null)
                        contact.UnreadCount = 0;
                }

                ScheduleSave(bareJid, messages);

                // Уведомление в трее, если окно неактивно
                if (!IsActive)
                {
                    
                    _trayIcon.ShowBalloonTip("Новое сообщение",
                        $"{bareJid}: {realText.Truncate(50)}",
                        BalloonIcon.Info);
                    Console.WriteLine("Показано уведомление в трее.");
                }
            });
        }

        private void EditMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is ChatMessage message)
            {
                if (message.Author != "Я")
                {
                    MessageBox.Show("Нельзя редактировать сообщения собеседника!");
                    return;
                }

                if ((DateTime.Now - message.Time).TotalMinutes > 3)
                {
                    MessageBox.Show("Редактирование сообщений разрешено только в течение 3 минут после отправки.");
                    return;
                }

                _editingMessage = message;
                MessageRichBox.Document.Blocks.Clear();
                MessageRichBox.Document.Blocks.Add(new Paragraph(new Run(message.Text)));
                MessageRichBox.Focus();
            }
        }





        private async void SendMessage_Click(object sender, RoutedEventArgs e)
        {

            if (_client == null || !_client.Connected)
                return;

            var chatTab = ChatTabs.SelectedItem as ChatTab;
            if (chatTab == null)
            {
                MessageBox.Show("Выберите чат перед отправкой сообщения!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var messages = chatTab.Content as ObservableCollection<ChatMessage>;
            string markdownText = GetMarkdownFromRichTextBox();

            // --- если редактируем сообщение ---
            if (_editingMessage != null)
            {
                _editingMessage.Text = markdownText;
                _editingMessage.Time = DateTime.Now;
                _editingMessage.IsEdited = true;
                _editingMessage.OnPropertyChanged(nameof(ChatMessage.DisplayText));
                ScheduleSave(chatTab.Jid, messages);

                string payload = $"##edit:{_editingMessage.Id}##{_editingMessage.Text}";
                _client.SendMessage(new Jid(chatTab.Jid), payload);

                _editingMessage = null;
                _selectedFilePath = null;
                MessageRichBox.Document.Blocks.Clear();

                return;
            }

            // --- если прикреплён файл (но ещё не отправлен) ---
            if (FilePreviewPanel.Visibility == Visibility.Visible && !string.IsNullOrEmpty(_selectedFilePath))
            {
                try
                {
                    FileUploadProgressBar.Visibility = Visibility.Visible;
                    FileUploadProgressBar.Value = 0;

                    var progress = new Progress<double>(p => FileUploadProgressBar.Value = p);
                    string uploadedFileName = await UploadFileAsync(_selectedFilePath, progress);

                    FileUploadProgressBar.Visibility = Visibility.Collapsed;

                    string fileName = Path.GetFileName(_selectedFilePath);
                    string fileUrl = "http://win-m4f2mfrj6i4.vkr.loc/fileschat/files/" + uploadedFileName;

                    string msgId = Guid.NewGuid().ToString("N");
                    var fileMessage = new ChatMessage
                    {
                        Id = msgId,
                        Author = "Я",
                        Time = DateTime.Now,
                        IsIncoming = false,
                        Status = MessageStatus.Sent,
                        Text = $"[{fileName}]({fileUrl})",
                        FileName = fileName,
                        FileUrl = fileUrl
                    };

                    messages.Add(fileMessage);
                    ScheduleSave(chatTab.Jid, messages);

                    string payload = MetaPrefixId + msgId + "##" +
                        (fileMessage.Text.StartsWith("##") ? MetaEscape + fileMessage.Text : fileMessage.Text);
                    _client.SendMessage(new Jid(chatTab.Jid), payload);

                    // Очистка панели
                    FilePreviewPanel.Visibility = Visibility.Collapsed;
                    FileUploadProgressBar.Value = 0;
                    _selectedFilePath = null;
                    _uploadedFileName = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при загрузке файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    FileUploadProgressBar.Visibility = Visibility.Collapsed;
                }

                return; // выходим, чтобы не отправить текст вместе
            }

            // --- обычное текстовое сообщение ---
            if (messages != null && !string.IsNullOrWhiteSpace(markdownText))
            {
                try
                {
                    string msgId = Guid.NewGuid().ToString("N");
                    string payload = MetaPrefixId + msgId + "##" +
                        (markdownText.StartsWith("##") ? MetaEscape + markdownText : markdownText);

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
        

        private void MessagesList_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                _dragDepth++;
                DropOverlay.Visibility = Visibility.Visible; // показываем только один раз
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MessagesList_DragLeave(object sender, DragEventArgs e)
        {
            _dragDepth--;
            if (_dragDepth <= 0)
            {
                _dragDepth = 0;
                DropOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void MessagesList_Drop(object sender, DragEventArgs e)
        {
            _dragDepth = 0; // сбрасываем
            DropOverlay.Visibility = Visibility.Collapsed;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 0)
                    return;

                string filePath = files[0];
                var fileInfo = new FileInfo(filePath);

                var chatTab = ChatTabs.SelectedItem as ChatTab;
                if (chatTab == null)
                {
                    MessageBox.Show("Выберите чат перед прикреплением файла!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Показываем карточку предпросмотра
                FilePreviewPanel.Visibility = Visibility.Visible;
                FileUploadProgressBar.Visibility = Visibility.Collapsed;
                FileUploadProgressBar.Value = 0;

                SelectedFileName.Text = fileInfo.Name;
                SelectedFileSize.Text = $"{fileInfo.Length / 1024.0 / 1024.0:F2} MB";
                _selectedFilePath = filePath;
                _uploadedFileName = null;
            }
        }

        private void DropOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Чтобы по клику на затемнение оно исчезало
            DropOverlay.Visibility = Visibility.Collapsed;
        }
        private void CancelAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            FilePreviewPanel.Visibility = Visibility.Collapsed;
            FileUploadProgressBar.Value = 0;
            _selectedFilePath = null;
            _uploadedFileName = null;
        }
        

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                _isDraggingFile = true;
                DropOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (_isDraggingFile && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DropOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            _isDraggingFile = false;
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            _isDraggingFile = false;
            DropOverlay.Visibility = Visibility.Collapsed;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 0)
                    return;

                string filePath = files[0];
                var fileInfo = new FileInfo(filePath);

                var chatTab = ChatTabs.SelectedItem as ChatTab;
                if (chatTab == null)
                {
                    MessageBox.Show("Выберите чат перед прикреплением файла!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Показываем карточку предпросмотра
                FilePreviewPanel.Visibility = Visibility.Visible;
                FileUploadProgressBar.Visibility = Visibility.Collapsed;
                FileUploadProgressBar.Value = 0;

                SelectedFileName.Text = fileInfo.Name;
                SelectedFileSize.Text = $"{fileInfo.Length / 1024.0 / 1024.0:F2} MB";
                _selectedFilePath = filePath;
                _uploadedFileName = null;
            }
        }




        private void ReplyMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ChatMessage message)
            {
                // Добавляем цитату в RichTextBox
                string quote = $"> {message.Author}: {message.Text}\n\n";
                MessageRichBox.Document.Blocks.Clear();
                MessageRichBox.Document.Blocks.Add(new Paragraph(new Run(quote)));
                MessageRichBox.CaretPosition = MessageRichBox.Document.ContentEnd;
                MessageRichBox.Focus();
            }
        }




        private void CloseTab(ChatTab tab)
        {
            if (tab != null && ChatTabsItems.Contains(tab))
                ChatTabsItems.Remove(tab);
        }

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_client == null || !_client.Connected)
                return;

            var selectedItem = StatusComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null)
                return;

            string tag = selectedItem.Tag?.ToString();
            Availability availability;
            if (tag == "Online")
                availability = Availability.Online;
            else if (tag == "Away")
                availability = Availability.Away;
            else
                availability = Availability.Online;

            // обновляем статус в XMPP
            UpdateStatus(availability, StatusMessageBox?.Text ?? "");

            // принудительно обновляем цвет шарика
            string selfJid = _client.Jid.Node + "@" + _client.Jid.Domain;
            var selfContact = Contacts.FirstOrDefault(c => c.Jid == selfJid);
            if (selfContact != null)
            {
                selfContact.Availability = availability;
            }

            if (_isAutomaticChange)
            {
                _isAutomaticChange = false;  // Сбрасываем флаг после автоматического изменения
            }
            else
            {
                _manualStatusSet = true;  // Устанавливаем только для ручных изменений
            }
        }

        private void OpenAttachments_Click(object sender, RoutedEventArgs e)
        {
            if (ChatTabs.SelectedItem is ChatTab tab)
            {
                // ⚙️ Получаем JID из вкладки
                var jid = tab.Jid;
                if (string.IsNullOrEmpty(jid))
                {
                    MessageBox.Show("Не удалось определить пользователя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 🧩 Открываем окно вложений по JID
                var win = new AttachmentsWindow(jid);
                win.Owner = this;
                win.Show();
            }
            else
            {
                MessageBox.Show("Не выбран активный чат.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }




        private void StatusMessageBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var availability = GetUiSelectedAvailability();
            UpdateStatus(availability, StatusMessageBox?.Text ?? "");

            // Сохраняем текст в файл
            try
            {
                File.WriteAllText(StatusFilePath, StatusMessageBox.Text, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка сохранения статуса: " + ex.Message);
            }
        }

        private void ChatWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(StatusFilePath))
                {
                    StatusMessageBox.Text = File.ReadAllText(StatusFilePath, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка загрузки сохранённого статуса: " + ex.Message);
            }
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
                // Сбрасываем UnreadCount сразу
                selectedTab.UnreadCount = 0;
                var contact = ContactGroups.SelectMany(g => g.Contacts)
                                           .FirstOrDefault(c => c.Jid == selectedTab.Jid);
                if (contact != null)
                    contact.UnreadCount = 0;

                if (selectedTab.Content is ObservableCollection<ChatMessage> list)
                {
                    var unreadIncoming = list
                        .Where(m => m.IsIncoming && m.Status == MessageStatus.Sent && !string.IsNullOrEmpty(m.Id))
                        .ToArray();

                    foreach (var msg in unreadIncoming)
                        msg.Status = MessageStatus.Read;

                    ScheduleSave(selectedTab.Jid, list);

                    // Отправляем receipt
                    foreach (var msg in unreadIncoming)
                    {
                        try
                        {
                            string receiptPayload = "##receipt:" + msg.Id + "##";
                            _client.SendMessage(new Jid(selectedTab.Jid), receiptPayload);
                        }
                        catch { }
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

        public void ScheduleSave(string jid, ObservableCollection<ChatMessage> messages)
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
            string search = SearchTextBox.Text?.Trim().ToLower() ?? "";

            foreach (var group in ContactGroups)
            {
                foreach (var contact in group.Contacts)
                {
                    contact.IsVisible = string.IsNullOrEmpty(search)
                        || contact.Name.ToLower().Contains(search)
                        || contact.Jid.ToLower().Contains(search);
                }
            }
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
        private int _unreadCount = 0;
        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                if (_unreadCount != value)
                {
                    _unreadCount = value;
                    OnPropertyChanged(nameof(UnreadCount));
                    OnPropertyChanged(nameof(UnreadIndicatorVisibility));
                }
            }
        }

        public Visibility UnreadIndicatorVisibility => UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        
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
    public class AuthorToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is string author && author == "Я") ? Visibility.Visible : Visibility.Collapsed;
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