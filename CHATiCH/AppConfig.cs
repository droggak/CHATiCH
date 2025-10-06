using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CHATiCH
{
    public class AppConfig
    {
        public string Username { get; set; }
        public string Domain { get; set; }
        public string EncryptedPassword { get; set; }
        public bool RememberMe { get; set; }
        public bool RememberPassword { get; set; }

        private static string ConfigPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                         "CHATiCH_config.json");

        // 🔒 Шифрование/дешифровка с использованием DPAPI (надёжно и просто)
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return null;

            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public static string Decrypt(string encryptedText)
        {
            try
            {
                byte[] data = Convert.FromBase64String(encryptedText);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null;
            }
        }

        // 💾 Сохранение и загрузка
        public void Save()
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        public static AppConfig Load()
        {
            if (!File.Exists(ConfigPath))
                return null;

            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json);
        }

        public static bool Exists() => File.Exists(ConfigPath);
    }
}
