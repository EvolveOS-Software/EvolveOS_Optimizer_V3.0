// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using EvolveOS_Optimizer.Utilities.Configuration;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class AesHelper
    {
        private const int KeySize = 256;
        private const int SaltSize = 16;

        private const int GcmNonceSize = 12;
        private const int GcmTagSize = 16;

        private const string ApplicationPepper = "E-Ke:(8@h5}WPn@#:Jl:}fD8gHa$04-L,P-OS";

        private static string ConvertToPlainText(SecureString secureString)
        {
            if (secureString == null)
            {
                return string.Empty;
            }

            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocAnsi(secureString);
                return Marshal.PtrToStringAnsi(unmanagedString) ?? string.Empty;
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocAnsi(unmanagedString);
                }
            }
        }

        private static string ApplyPepper(string password)
        {
            return password + ApplicationPepper;
        }

        #region String-Based Methods (Used by Password Manager)

        public static string Encrypt(string plainText, SecureString masterSecurePassword, KeyDerivationConfig config)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            string password = ConvertToPlainText(masterSecurePassword);
            byte[] salt;

            using (var saltGenerator = RandomNumberGenerator.Create())
            {
                salt = new byte[SaltSize];
                saltGenerator.GetBytes(salt);
            }

            return Encrypt(plainText, password, salt, config);
        }

        public static string Encrypt(string plainText, SecureString masterSecurePassword)
        {
            return Encrypt(plainText, masterSecurePassword, KeyDerivationConfig.Create(KeyDerivationMode.Balanced));
        }

        public static string Decrypt(string encryptedBase64, SecureString masterSecurePassword, KeyDerivationConfig config)
        {
            if (string.IsNullOrEmpty(encryptedBase64))
            {
                return string.Empty;
            }

            try
            {
                string password = ConvertToPlainText(masterSecurePassword);

                byte[] combined = Convert.FromBase64String(encryptedBase64);

                if (combined.Length < SaltSize)
                {
                    throw new CryptographicException("Encrypted data is too short.");
                }

                byte[] salt = new byte[SaltSize];
                Buffer.BlockCopy(combined, 0, salt, 0, SaltSize);

                return Decrypt(encryptedBase64, password, salt, config);
            }
            catch (CryptographicException cryptEx)
            {
                throw new Exception("Decryption failed due to invalid master password or corrupted data.", cryptEx);
            }
            catch (Exception ex)
            {
                throw new Exception("General decryption error.", ex);
            }
        }

        public static string Decrypt(string encryptedBase64, SecureString masterSecurePassword)
        {
            return Decrypt(encryptedBase64, masterSecurePassword, KeyDerivationConfig.Create(KeyDerivationMode.Balanced));
        }

        public static string Encrypt(string plainText, string password, byte[] salt, KeyDerivationConfig config)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            if (salt == null || salt.Length != SaltSize)
            {
                throw new ArgumentException($"Salt must be {SaltSize} bytes.", nameof(salt));
            }

            config ??= KeyDerivationConfig.Default;

            string keyPassword = ApplyPepper(password);

            byte[] key = Rfc2898DeriveBytes.Pbkdf2(
                keyPassword,
                salt,
                config.IterationCount,
                GetHashAlgorithm(config.HashAlgorithm),
                config.KeySize);

            if (config.UseGcm)
            {
                byte[] nonce = new byte[GcmNonceSize];

                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(nonce);
                }

                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

                ICipherParameters parameters = new AeadParameters(new KeyParameter(key), GcmTagSize * 8, nonce);

                GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
                cipher.Init(true, parameters);

                byte[] cipherBytes = new byte[cipher.GetOutputSize(plainBytes.Length)];

                int outLength = cipher.ProcessBytes(plainBytes, 0, plainBytes.Length, cipherBytes, 0);
                outLength += cipher.DoFinal(cipherBytes, outLength);

                byte[] ciphertext = new byte[outLength - GcmTagSize];
                byte[] tag = new byte[GcmTagSize];

                Buffer.BlockCopy(cipherBytes, 0, ciphertext, 0, ciphertext.Length);
                Buffer.BlockCopy(cipherBytes, ciphertext.Length, tag, 0, GcmTagSize);

                byte[] result = new byte[SaltSize + GcmNonceSize + GcmTagSize + ciphertext.Length];

                Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
                Buffer.BlockCopy(nonce, 0, result, SaltSize, GcmNonceSize);
                Buffer.BlockCopy(tag, 0, result, SaltSize + GcmNonceSize, GcmTagSize);
                Buffer.BlockCopy(ciphertext, 0, result, SaltSize + GcmNonceSize + GcmTagSize, ciphertext.Length);

                return Convert.ToBase64String(result);
            }
            else
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.KeySize = config.KeySize * 8;
                    aesAlg.BlockSize = 128;
                    aesAlg.Mode = CipherMode.CBC;
                    aesAlg.Padding = PaddingMode.PKCS7;
                    aesAlg.Key = key;

                    aesAlg.GenerateIV();
                    byte[] iv = aesAlg.IV;

                    ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        msEncrypt.Write(salt, 0, salt.Length);
                        msEncrypt.Write(iv, 0, iv.Length);

                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                            {
                                swEncrypt.Write(plainText);
                            }
                        }

                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
        }

        public static string Decrypt(string cipherText, string password, byte[] salt, KeyDerivationConfig config)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            if (salt == null || salt.Length != SaltSize)
            {
                throw new ArgumentException($"Salt must be {SaltSize} bytes.", nameof(salt));
            }

            config ??= KeyDerivationConfig.Default;

            byte[] fullBytes = Convert.FromBase64String(cipherText);

            string keyPassword = ApplyPepper(password);

            byte[] key = Rfc2898DeriveBytes.Pbkdf2(
                keyPassword,
                salt,
                config.IterationCount,
                GetHashAlgorithm(config.HashAlgorithm),
                config.KeySize);

            if (config.UseGcm)
            {
                int minLen = SaltSize + GcmNonceSize + GcmTagSize;
                if (fullBytes.Length < minLen)
                {
                    throw new CryptographicException("Encrypted data is too short for GCM.");
                }

                byte[] nonce = new byte[GcmNonceSize];
                Buffer.BlockCopy(fullBytes, SaltSize, nonce, 0, GcmNonceSize);

                byte[] tag = new byte[GcmTagSize];
                Buffer.BlockCopy(fullBytes, SaltSize + GcmNonceSize, tag, 0, GcmTagSize);

                int cipherLen = fullBytes.Length - minLen;
                byte[] cipherBytes = new byte[cipherLen];
                Buffer.BlockCopy(fullBytes, minLen, cipherBytes, 0, cipherLen);

                byte[] bcInput = new byte[cipherLen + GcmTagSize];
                Buffer.BlockCopy(cipherBytes, 0, bcInput, 0, cipherLen);
                Buffer.BlockCopy(tag, 0, bcInput, cipherLen, GcmTagSize);

                byte[] plainBytes = new byte[cipherLen];

                try
                {
                    ICipherParameters parameters = new AeadParameters(new KeyParameter(key), GcmTagSize * 8, nonce);

                    GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
                    cipher.Init(false, parameters);

                    int outLength = cipher.ProcessBytes(bcInput, 0, bcInput.Length, plainBytes, 0);
                    outLength += cipher.DoFinal(plainBytes, outLength);

                    if (outLength != plainBytes.Length)
                    {
                        byte[] result = new byte[outLength];
                        Buffer.BlockCopy(plainBytes, 0, result, 0, outLength);
                        plainBytes = result;
                    }
                }
                catch (Org.BouncyCastle.Crypto.InvalidCipherTextException)
                {
                    throw new CryptographicException("Authentication failed. Data has been tampered with or password is incorrect.");
                }

                return Encoding.UTF8.GetString(plainBytes);
            }
            else
            {
                if (fullBytes.Length < SaltSize + config.IvSize)
                {
                    throw new CryptographicException("Encrypted data is too short or configuration is incorrect.");
                }

                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.KeySize = config.KeySize * 8;
                    aesAlg.BlockSize = 128;
                    aesAlg.Mode = CipherMode.CBC;
                    aesAlg.Padding = PaddingMode.PKCS7;
                    aesAlg.Key = key;

                    byte[] iv = new byte[config.IvSize];
                    Buffer.BlockCopy(fullBytes, SaltSize, iv, 0, config.IvSize);

                    aesAlg.IV = iv;

                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    int cipherStartIndex = SaltSize + config.IvSize;
                    byte[] cipherBytes = new byte[fullBytes.Length - cipherStartIndex];
                    Buffer.BlockCopy(fullBytes, cipherStartIndex, cipherBytes, 0, cipherBytes.Length);

                    using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                return srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Byte Array Methods (Used by File/Folder Encryptor)

        public static byte[] EncryptBytes(byte[] plainBytes, SecureString masterSecurePassword, KeyDerivationConfig config)
        {
            if (plainBytes == null || plainBytes.Length == 0) return Array.Empty<byte>();

            string password = ConvertToPlainText(masterSecurePassword);
            byte[] salt;

            using (var saltGenerator = RandomNumberGenerator.Create())
            {
                salt = new byte[SaltSize];
                saltGenerator.GetBytes(salt);
            }

            return EncryptBytes(plainBytes, password, salt, config);
        }

        public static byte[] EncryptBytes(byte[] plainBytes, SecureString masterSecurePassword)
        {
            return EncryptBytes(plainBytes, masterSecurePassword, KeyDerivationConfig.Create(KeyDerivationMode.Balanced));
        }

        public static byte[] DecryptBytes(byte[] encryptedBytes, SecureString masterSecurePassword, KeyDerivationConfig config)
        {
            if (encryptedBytes == null || encryptedBytes.Length == 0) return Array.Empty<byte>();

            try
            {
                string password = ConvertToPlainText(masterSecurePassword);

                if (encryptedBytes.Length < SaltSize)
                {
                    throw new CryptographicException("Encrypted data is too short.");
                }

                byte[] salt = new byte[SaltSize];
                Buffer.BlockCopy(encryptedBytes, 0, salt, 0, SaltSize);

                return DecryptBytes(encryptedBytes, password, salt, config);
            }
            catch (CryptographicException cryptEx)
            {
                throw new Exception("Decryption failed due to invalid master password or corrupted data.", cryptEx);
            }
            catch (Exception ex)
            {
                throw new Exception("General decryption error.", ex);
            }
        }

        public static byte[] DecryptBytes(byte[] encryptedBytes, SecureString masterSecurePassword)
        {
            return DecryptBytes(encryptedBytes, masterSecurePassword, KeyDerivationConfig.Create(KeyDerivationMode.Balanced));
        }

        public static byte[] EncryptBytes(byte[] plainBytes, string password, byte[] salt, KeyDerivationConfig config)
        {
            if (plainBytes == null || plainBytes.Length == 0) return Array.Empty<byte>();
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            if (salt == null || salt.Length != SaltSize) throw new ArgumentException($"Salt must be {SaltSize} bytes.", nameof(salt));

            config ??= KeyDerivationConfig.Default;
            string keyPassword = ApplyPepper(password);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(keyPassword, salt, config.IterationCount, GetHashAlgorithm(config.HashAlgorithm), config.KeySize);

            if (config.UseGcm)
            {
                byte[] nonce = new byte[GcmNonceSize];
                using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(nonce); }

                ICipherParameters parameters = new AeadParameters(new KeyParameter(key), GcmTagSize * 8, nonce);
                GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
                cipher.Init(true, parameters);

                byte[] cipherBytes = new byte[cipher.GetOutputSize(plainBytes.Length)];
                int outLength = cipher.ProcessBytes(plainBytes, 0, plainBytes.Length, cipherBytes, 0);
                outLength += cipher.DoFinal(cipherBytes, outLength);

                byte[] ciphertext = new byte[outLength - GcmTagSize];
                byte[] tag = new byte[GcmTagSize];

                Buffer.BlockCopy(cipherBytes, 0, ciphertext, 0, ciphertext.Length);
                Buffer.BlockCopy(cipherBytes, ciphertext.Length, tag, 0, GcmTagSize);

                byte[] result = new byte[SaltSize + GcmNonceSize + GcmTagSize + ciphertext.Length];
                Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
                Buffer.BlockCopy(nonce, 0, result, SaltSize, GcmNonceSize);
                Buffer.BlockCopy(tag, 0, result, SaltSize + GcmNonceSize, GcmTagSize);
                Buffer.BlockCopy(ciphertext, 0, result, SaltSize + GcmNonceSize + GcmTagSize, ciphertext.Length);

                return result;
            }
            else
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.KeySize = config.KeySize * 8;
                    aesAlg.BlockSize = 128;
                    aesAlg.Mode = CipherMode.CBC;
                    aesAlg.Padding = PaddingMode.PKCS7;
                    aesAlg.Key = key;
                    aesAlg.GenerateIV();
                    byte[] iv = aesAlg.IV;

                    using (ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV))
                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        msEncrypt.Write(salt, 0, salt.Length);
                        msEncrypt.Write(iv, 0, iv.Length);
                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(plainBytes, 0, plainBytes.Length);
                        }
                        return msEncrypt.ToArray();
                    }
                }
            }
        }

        public static byte[] DecryptBytes(byte[] fullBytes, string password, byte[] salt, KeyDerivationConfig config)
        {
            if (fullBytes == null || fullBytes.Length == 0) return Array.Empty<byte>();
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            if (salt == null || salt.Length != SaltSize) throw new ArgumentException($"Salt must be {SaltSize} bytes.", nameof(salt));

            config ??= KeyDerivationConfig.Default;
            string keyPassword = ApplyPepper(password);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(keyPassword, salt, config.IterationCount, GetHashAlgorithm(config.HashAlgorithm), config.KeySize);

            if (config.UseGcm)
            {
                int minLen = SaltSize + GcmNonceSize + GcmTagSize;
                if (fullBytes.Length < minLen) throw new CryptographicException("Encrypted data is too short for GCM.");

                byte[] nonce = new byte[GcmNonceSize];
                Buffer.BlockCopy(fullBytes, SaltSize, nonce, 0, GcmNonceSize);

                byte[] tag = new byte[GcmTagSize];
                Buffer.BlockCopy(fullBytes, SaltSize + GcmNonceSize, tag, 0, GcmTagSize);

                int cipherLen = fullBytes.Length - minLen;
                byte[] cipherBytes = new byte[cipherLen];
                Buffer.BlockCopy(fullBytes, minLen, cipherBytes, 0, cipherLen);

                byte[] bcInput = new byte[cipherLen + GcmTagSize];
                Buffer.BlockCopy(cipherBytes, 0, bcInput, 0, cipherLen);
                Buffer.BlockCopy(tag, 0, bcInput, cipherLen, GcmTagSize);

                byte[] plainBytes = new byte[cipherLen];

                try
                {
                    ICipherParameters parameters = new AeadParameters(new KeyParameter(key), GcmTagSize * 8, nonce);
                    GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
                    cipher.Init(false, parameters);

                    int outLength = cipher.ProcessBytes(bcInput, 0, bcInput.Length, plainBytes, 0);
                    outLength += cipher.DoFinal(plainBytes, outLength);

                    if (outLength != plainBytes.Length)
                    {
                        byte[] result = new byte[outLength];
                        Buffer.BlockCopy(plainBytes, 0, result, 0, outLength);
                        return result;
                    }
                    return plainBytes;
                }
                catch (Org.BouncyCastle.Crypto.InvalidCipherTextException)
                {
                    throw new CryptographicException("Authentication failed. Data tampered with or incorrect password.");
                }
            }
            else
            {
                if (fullBytes.Length < SaltSize + config.IvSize) throw new CryptographicException("Encrypted data is too short.");

                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.KeySize = config.KeySize * 8;
                    aesAlg.BlockSize = 128;
                    aesAlg.Mode = CipherMode.CBC;
                    aesAlg.Padding = PaddingMode.PKCS7;
                    aesAlg.Key = key;

                    byte[] iv = new byte[config.IvSize];
                    Buffer.BlockCopy(fullBytes, SaltSize, iv, 0, config.IvSize);
                    aesAlg.IV = iv;

                    using (ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV))
                    {
                        int cipherStartIndex = SaltSize + config.IvSize;
                        int cipherLen = fullBytes.Length - cipherStartIndex;

                        using (MemoryStream msDecrypt = new MemoryStream(fullBytes, cipherStartIndex, cipherLen))
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        using (MemoryStream msPlain = new MemoryStream())
                        {
                            csDecrypt.CopyTo(msPlain);
                            return msPlain.ToArray();
                        }
                    }
                }
            }
        }

        #endregion

        private static HashAlgorithmName GetHashAlgorithm(string hashAlgorithm)
        {
            return hashAlgorithm.ToUpperInvariant() switch
            {
                "SHA1" => HashAlgorithmName.SHA1,
                "SHA256" => HashAlgorithmName.SHA256,
                "SHA384" => HashAlgorithmName.SHA384,
                "SHA512" => HashAlgorithmName.SHA512,
                _ => throw new ArgumentException("Invalid hash algorithm name provided.")
            };
        }
    }
}