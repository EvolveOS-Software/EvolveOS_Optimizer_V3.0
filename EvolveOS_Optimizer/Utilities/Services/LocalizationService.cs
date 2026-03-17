// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public enum StringStatus { Found, Fallback, Missing }

    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly Dictionary<string, string> _defaultCache = new();
        private readonly Dictionary<string, string> _targetCache = new();

        private readonly Dictionary<string, string> _missingStringsLog = new();
        private readonly object _logLock = new();
        private string _currentLanguage = "en-us";

        private static string RealBaseDir
        {
            get
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                return Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            }
        }

        public LocalizationService()
        {
            EnsureLanguageFilesExistLocally();

            LoadDefaultLanguage();
        }

        private void EnsureLanguageFilesExistLocally()
        {
            string langDir = Path.Combine(RealBaseDir, "Languages");

            try
            {
                if (!Directory.Exists(langDir))
                {
                    Directory.CreateDirectory(langDir);
                }

                var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                string resourcePrefix = "EvolveOS_Optimizer.Languages.";

                string[] resourceNames = assembly.GetManifestResourceNames();

                foreach (string resourceName in resourceNames)
                {
                    if (resourceName.StartsWith(resourcePrefix) && resourceName.EndsWith(".xaml"))
                    {
                        string fileName = resourceName.Substring(resourcePrefix.Length);
                        string filePath = Path.Combine(langDir, fileName);

                        if (!File.Exists(filePath))
                        {
                            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                            {
                                if (stream != null)
                                {
                                    using (FileStream fileStream = File.Create(filePath))
                                    {
                                        stream.CopyTo(fileStream);
                                    }
                                    Debug.WriteLine($"[Localization] Successfully extracted {fileName}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Localization] Failed to extract local language files: {ex.Message}");
            }
        }

        public string Get(string key) => GetWithStatus(key).Value;
        public string this[string key] => GetWithStatus(key).Value;

        public (string Value, StringStatus Status) GetWithStatus(string key)
        {
            if (string.IsNullOrEmpty(key)) return (string.Empty, StringStatus.Found);

            if (_targetCache.TryGetValue(key, out var targetValue))
            {
                return (targetValue, StringStatus.Found);
            }

            if (_defaultCache.TryGetValue(key, out var defaultValue))
            {
                LogMissingString(key, defaultValue);
                return (defaultValue, StringStatus.Fallback);
            }

            LogMissingString(key, "");
            return ($"[{key}]", StringStatus.Missing);
        }

        private void LoadDefaultLanguage()
        {
            string filePath = Path.Combine(RealBaseDir, "Languages", "en-us.xaml");
            LoadDictionary(filePath, _defaultCache);
        }

        public void LoadLanguage(string langCode)
        {
            _currentLanguage = langCode.ToLower();

            string filePath = Path.Combine(RealBaseDir, "Languages", $"{_currentLanguage}.xaml");

            _targetCache.Clear();
            LoadDictionary(filePath, _targetCache);

            Refresh();
        }

        private void LoadDictionary(string filePath, Dictionary<string, string> cache)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                XDocument doc = XDocument.Load(filePath);
                XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

                foreach (var element in doc.Descendants())
                {
                    var keyAttr = element.Attribute(x + "Key");
                    if (keyAttr != null)
                    {
                        cache[keyAttr.Value] = element.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Localization] XML Read Error for {filePath}: {ex.Message}");
            }
        }

        private void LogMissingString(string key, string fallbackValue)
        {
            if (!LocalMachineSettingsEngine.IsDeveloperMode) return;

            if (_currentLanguage == "en-us") return;

            lock (_logLock)
            {
                if (!_missingStringsLog.ContainsKey(key))
                {
                    _missingStringsLog[key] = string.IsNullOrEmpty(fallbackValue) ? "NEEDS_TRANSLATION" : fallbackValue;

                    try
                    {
                        string logDir = Path.Combine(RealBaseDir, "Languages");
                        Directory.CreateDirectory(logDir);
                        string logPath = Path.Combine(logDir, $"MissingStrings_{_currentLanguage}.json");

                        var options = new JsonSerializerOptions { WriteIndented = true };
                        string json = JsonSerializer.Serialize(_missingStringsLog, options);
                        File.WriteAllText(logPath, json);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Localization] Failed to write JSON log: {ex.Message}");
                    }
                }
            }
        }

        public void Refresh() => OnPropertyChanged("Item[]");

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}