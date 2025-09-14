using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CHATiCH
{
    public static class HistoryManager
    {
        private static readonly string HistoryRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CHATiCH", "History");

        private static string GetHistoryFile(string jid, DateTime date)
        {
            string dayFolder = date.ToString("yyyy-MM-dd");
            string folder = Path.Combine(HistoryRoot, dayFolder);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return Path.Combine(folder, $"{jid.Replace("@", "_at_")}.json");
        }

        public static ObservableCollection<ChatMessage> LoadHistory(string jid, DateTime? date = null)
        {
            try
            {
                string file = GetHistoryFile(jid, date ?? DateTime.Now);
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

        public static void SaveHistory(string jid, ObservableCollection<ChatMessage> messages, DateTime? date = null)
        {
            try
            {
                string file = GetHistoryFile(jid, date ?? DateTime.Now);
                string json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка сохранения истории: " + ex.Message);
            }
        }

        public static string[] GetAvailableDates(string jid)
        {
            if (!Directory.Exists(HistoryRoot))
                return Array.Empty<string>();

            return Directory.GetDirectories(HistoryRoot)
                .Where(d => File.Exists(Path.Combine(d, $"{jid.Replace("@", "_at_")}.json")))
                .Select(Path.GetFileName)
                .ToArray();
        }

        public static void CleanupOldHistory(int keepDays = 365)
        {
            if (!Directory.Exists(HistoryRoot))
                return;

            foreach (var dir in Directory.GetDirectories(HistoryRoot))
            {
                if (DateTime.TryParse(Path.GetFileName(dir), out var dirDate))
                {
                    if ((DateTime.Now - dirDate).TotalDays > keepDays)
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                }
            }
        }
    }
}
