using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Bouncycastleconnector;
using iText.Bouncycastle;

namespace CHATiCH
{
    public partial class HistoryWindow : Window
    {
        public string Jid { get; set; }
        public ObservableCollection<ChatMessage> Messages { get; set; } = new ObservableCollection<ChatMessage>();

        public HistoryWindow(string jid)
        {
            InitializeComponent();
            Jid = jid;
            DataContext = this;
            LoadDates();
        }

        private void LoadDates()
        {
            var dates = HistoryManager.GetAvailableDates(Jid);
            DatesList.ItemsSource = dates;
        }

        private void DatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DatesList.SelectedItem is string dateStr && DateTime.TryParse(dateStr, out DateTime date))
            {
                Messages.Clear();
                var hist = HistoryManager.LoadHistory(Jid, date);
                foreach (var msg in hist) Messages.Add(msg);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt|PDF file (*.pdf)|*.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                if (dialog.FileName.EndsWith(".txt"))
                {
                    // Экспорт в текстовый файл
                    string content = string.Join("\n", Messages.Select(m => m.ToString()));
                    File.WriteAllText(dialog.FileName, content);
                }
                else if (dialog.FileName.EndsWith(".pdf"))
                {
                    // Экспорт в PDF (iText7)
                    using (var writer = new PdfWriter(dialog.FileName))
                    using (var pdf = new PdfDocument(writer))
                    using (var doc = new Document(pdf))
                    {
                        foreach (var msg in Messages)
                        {
                            doc.Add(new Paragraph(msg.ToString()));
                        }
                    }
                }

                MessageBox.Show("Экспорт завершён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

    }
}