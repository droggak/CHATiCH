using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Security
{
    internal static class AesJsonCrypto
    {
        private static readonly byte[] Key =
            Encoding.UTF8.GetBytes("B40181E91A297DE0F627FCC5F6005C7F");

        public static byte[] Encrypt(string json)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = Key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                    byte[] cipherBytes = encryptor.TransformFinalBlock(
                        plainBytes, 0, plainBytes.Length);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(aes.IV, 0, aes.IV.Length);
                        ms.Write(cipherBytes, 0, cipherBytes.Length);
                        return ms.ToArray();
                    }
                }
            }
        }

        public static string Decrypt(byte[] encryptedData)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = Key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                byte[] iv = new byte[16];
                Array.Copy(encryptedData, iv, 16);
                aes.IV = iv;

                byte[] cipherBytes = new byte[encryptedData.Length - 16];
                Array.Copy(encryptedData, 16, cipherBytes, 0, cipherBytes.Length);

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    byte[] plainBytes = decryptor.TransformFinalBlock(
                        cipherBytes, 0, cipherBytes.Length);

                    return Encoding.UTF8.GetString(plainBytes);
                }
            }
        }
    }
}
