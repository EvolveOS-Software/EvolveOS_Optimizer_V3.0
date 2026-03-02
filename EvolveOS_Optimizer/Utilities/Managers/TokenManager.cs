// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    public static class TokenManager
    {
        private static readonly string TokenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auth.token");

        public static SecureString GetMachineKey()
        {
            string machineId = Environment.MachineName + Environment.ProcessorCount;
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId));
            }

            var secure = new SecureString();
            foreach (char c in Convert.ToBase64String(hash))
            {
                secure.AppendChar(c);
            }

            secure.MakeReadOnly();
            return secure;
        }

        public static void SaveToken(string username, string plainPassword)
        {
            try
            {
                using (SecureString machineKey = GetMachineKey())
                {
                    string encryptedPassword = AesHelper.Encrypt(plainPassword, machineKey);

                    string data = $"{username}|{encryptedPassword}";
                    File.WriteAllText(TokenPath, data);

                    Debug.WriteLine("Security token encrypted and saved successfully.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save encrypted token: {ex.Message}");
            }
        }

        public static (string? username, string? decryptedPassword) LoadAndDecryptToken()
        {
            try
            {
                if (!File.Exists(TokenPath))
                {
                    return (null, null);
                }

                string content = File.ReadAllText(TokenPath);
                var parts = content.Split('|');

                if (parts.Length != 2)
                {
                    return (null, null);
                }

                string username = parts[0];
                string encryptedPassword = parts[1];

                using (SecureString machineKey = GetMachineKey())
                {
                    string decryptedPassword = AesHelper.Decrypt(encryptedPassword, machineKey);
                    return (username, decryptedPassword);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to decrypt token: {ex.Message}");
                return (null, null);
            }
        }

        public static bool TokenExists()
        {
            string tokenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auth.token");
            return File.Exists(tokenPath);
        }

        public static void DeleteToken()
        {
            try
            {
                if (File.Exists(TokenPath))
                {
                    File.Delete(TokenPath);
                    Debug.WriteLine("Security token deleted successfully.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete token: {ex.Message}");
            }
        }
    }
}
