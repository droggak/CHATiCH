using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CHATiCH
{
    public static class HistoryManager
    {
        private static readonly string HistoryRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CHATiCH", "history");

        static HistoryManager()
        {
            Directory.CreateDirectory(HistoryRoot);
        }

        public static void SaveHistory(string jid, ObservableCollection<ChatMessage> messages)
        {
            if (string.IsNullOrEmpty(jid) || messages == null) return;

            string userFolder = Path.Combine(HistoryRoot, jid);
            Directory.CreateDirectory(userFolder);

            string fileName = Path.Combine(userFolder, DateTime.Now.ToString("yyyy-MM-dd") + ".json");
            try
            {
                File.WriteAllText(fileName, JsonConvert.SerializeObject(messages, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении истории {jid}: {ex.Message}");
            }
        }

        public static ObservableCollection<ChatMessage> LoadHistory(string jid, DateTime? date = null)
        {
            var result = new ObservableCollection<ChatMessage>();
            if (string.IsNullOrEmpty(jid)) return result;

            string userFolder = Path.Combine(HistoryRoot, jid);
            if (!Directory.Exists(userFolder)) return result;

            string fileName = Path.Combine(userFolder, (date ?? DateTime.Now).ToString("yyyy-MM-dd") + ".json");
            if (!File.Exists(fileName)) return result;

            try
            {
                string json = File.ReadAllText(fileName);
                var messages = JsonConvert.DeserializeObject<ObservableCollection<ChatMessage>>(json);
                if (messages != null)
                    foreach (var msg in messages)
                        result.Add(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при загрузке истории {jid}: {ex.Message}");
            }

            return result;
        }

        public static string[] GetAvailableDates(string jid)
        {
            string userFolder = Path.Combine(HistoryRoot, jid);
            if (!Directory.Exists(userFolder)) return Array.Empty<string>();

            return Directory.GetFiles(userFolder, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderByDescending(d => d)
                .ToArray();
        }

        public static void CleanupOldHistory()
        {
            if (!Directory.Exists(HistoryRoot)) return;

            foreach (var userFolder in Directory.GetDirectories(HistoryRoot))
            {
                foreach (var file in Directory.GetFiles(userFolder, "*.json"))
                {
                    if (DateTime.TryParse(Path.GetFileNameWithoutExtension(file), out var fileDate))
                    {
                        if ((DateTime.Now - fileDate).TotalDays > 30)
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                }
            }
        }
    }
}
