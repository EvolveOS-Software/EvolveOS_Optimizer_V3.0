// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Security.Cryptography;
using System.Threading;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public static class DatabaseSecurityService
    {
        private const string ApplicationPepper = "E-Ke:(8@h5}WPn@#:Jl:}fD8gHa$04-L,P-OS";
        private const int Iterations = 50000;
        private const int KeySize = 32;

        private static readonly string VaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EvolveOS", "db_vault.bin");

        private static byte[]? _masterSecret;

        private static byte[] GetDerivedKey(byte[] salt)
        {
            if (_masterSecret == null)
            {
                if (File.Exists(VaultPath))
                {
                    byte[] encryptedKey = File.ReadAllBytes(VaultPath);
                    _masterSecret = ProtectedData.Unprotect(encryptedKey, null, DataProtectionScope.CurrentUser);
                }
                else
                {
                    string? directoryPath = Path.GetDirectoryName(VaultPath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    _masterSecret = new byte[32];
                    using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(_masterSecret); }

                    byte[] encryptedKey = ProtectedData.Protect(_masterSecret, null, DataProtectionScope.CurrentUser);
                    File.WriteAllBytes(VaultPath, encryptedKey);
                }
            }

            string passwordWithPepper = Convert.ToBase64String(_masterSecret) + ApplicationPepper;

            return Rfc2898DeriveBytes.Pbkdf2(
                passwordWithPepper,
                salt,
                Iterations,
                HashAlgorithmName.SHA1,
                KeySize
            );
        }

        public static bool IsFileLocked(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }

        public static void EncryptDatabase(string plainFile, string encryptedFile)
        {
            if (!File.Exists(plainFile))
            {
                return;
            }

            int maxRetries = 10;
            int attempts = 0;
            while (IsFileLocked(plainFile) && attempts < maxRetries)
            {
                attempts++;
                Thread.Sleep(500);
            }

            if (IsFileLocked(plainFile))
            {
                throw new IOException($"File {plainFile} is still locked.");
            }

            try
            {
                byte[] salt = new byte[16];
                using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(salt); }

                byte[] key = GetDerivedKey(salt);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.GenerateIV();

                    using (FileStream fsOut = new FileStream(encryptedFile, FileMode.Create, FileAccess.Write))
                    {
                        fsOut.Write(salt, 0, salt.Length);
                        fsOut.Write(aes.IV, 0, aes.IV.Length);

                        using (var encryptor = aes.CreateEncryptor())
                        using (CryptoStream cs = new CryptoStream(fsOut, encryptor, CryptoStreamMode.Write))
                        using (FileStream fsIn = new FileStream(plainFile, FileMode.Open, FileAccess.Read))
                        {
                            fsIn.CopyTo(cs);
                            cs.FlushFinalBlock();
                        }
                    }
                }

                if (File.Exists(plainFile))
                {
                    SecureWipe(plainFile);
                    File.Delete(plainFile);
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(encryptedFile))
                {
                    try { File.Delete(encryptedFile); } catch { }
                }

                ErrorLogging.LogDebug(new Exception("Encryption failed. Plain file preserved for safety.", ex));

                throw;
            }
        }

        public static void DecryptDatabase(string encryptedFile, string plainFile)
        {
            if (!File.Exists(encryptedFile))
            {
                return;
            }

            try
            {
                using (FileStream fsIn = new FileStream(encryptedFile, FileMode.Open, FileAccess.Read))
                {
                    byte[] salt = new byte[16];
                    byte[] iv = new byte[16];

                    if (fsIn.Read(salt, 0, 16) < 16 || fsIn.Read(iv, 0, 16) < 16)
                    {
                        throw new CryptographicException("Fileheader is damaged.");
                    }

                    byte[] key = GetDerivedKey(salt);

                    using (Aes aes = Aes.Create())
                    {
                        aes.Key = key;
                        aes.IV = iv;

                        using (var decryptor = aes.CreateDecryptor())
                        using (CryptoStream cs = new CryptoStream(fsIn, decryptor, CryptoStreamMode.Read))
                        using (FileStream fsOut = new FileStream(plainFile, FileMode.Create, FileAccess.Write))
                        {
                            cs.CopyTo(fsOut);
                            fsOut.Flush();
                        }
                    }
                }
            }
            catch
            {
                if (File.Exists(plainFile))
                {
                    try { File.Delete(plainFile); } catch { }
                }
                throw;
            }
        }

        private static void SecureWipe(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                long length = new FileInfo(filePath).Length;

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[4096];
                    using (var rng = RandomNumberGenerator.Create())
                    {
                        long totalWritten = 0;
                        while (totalWritten < length)
                        {
                            rng.GetBytes(buffer);
                            int bytesToWrite = (int)Math.Min(buffer.Length, length - totalWritten);
                            fs.Write(buffer, 0, bytesToWrite);
                            totalWritten += bytesToWrite;
                        }
                    }
                    fs.Flush(true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Secure wipe failed: {ex.Message}");
            }
        }

        public static bool RestoreDatabase(string mdfPath, string ldfPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(mdfPath) ?? AppDomain.CurrentDomain.BaseDirectory;

                if (!HasWritePermission(dir))
                {
                    throw new UnauthorizedAccessException($"No write access to directory: {dir}");
                }

                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                bool filesRestored = false;

                if (!File.Exists(mdfPath))
                {
                    byte[] mdfBytes = GetResourceBytes("EvolveOS_OptimizerDb.mdf.gz");
                    ArchiveManager.Unarchive(mdfPath, mdfBytes);

                    var fileInfo = new FileInfo(mdfPath);

                    if (fileInfo.IsReadOnly)
                    {
                        fileInfo.IsReadOnly = false;
                    }

                    filesRestored = true;
                }

                if (!File.Exists(ldfPath))
                {
                    byte[] ldfBytes = GetResourceBytes("EvolveOS_OptimizerDb_log.ldf.gz");
                    ArchiveManager.Unarchive(ldfPath, ldfBytes);

                    var fileInfo = new FileInfo(ldfPath);

                    if (fileInfo.IsReadOnly)
                    {
                        fileInfo.IsReadOnly = false;
                    }

                    filesRestored = true;
                }

                return filesRestored;
            }
            catch (UnauthorizedAccessException uex)
            {
                ErrorLogging.LogDebug(uex, "Permission Error: Ensure the app has Write access to its folder.");
                return false;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex, "Extraction failed: " + ex.Message);
                return false;
            }
        }

        private static byte[] GetResourceBytes(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", fileName);
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var embeddedName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            if (embeddedName != null)
            {
                using var stream = assembly.GetManifestResourceStream(embeddedName);
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }

            throw new FileNotFoundException($"Could not find the resource '{fileName}'. Ensure it is in the Resources folder and Build Action is set to Content.");
        }

        private static bool HasWritePermission(string folderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath))
                {
                    return false;
                }

                if (!Directory.Exists(folderPath))
                {
                    return true;
                }

                string testFile = Path.Combine(folderPath, Guid.NewGuid().ToString() + ".tmp");
                using (FileStream fs = File.Create(testFile)) { }
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}