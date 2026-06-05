// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public enum StringStatus { Found, Fallback, Missing }

    public class LocalizationService : ILocalizationService, INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event EventHandler? LanguageChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly Dictionary<string, string> _defaultCache = new();
        private readonly Dictionary<string, string> _targetCache = new();
        private readonly Dictionary<string, string> _missingStringsLog = new();
        private readonly object _logLock = new();

        private string _currentLanguage = "en-us";
        private string _currentLanguageCode = "en-us";
        private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

        private static string RealBaseDir => Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? AppContext.BaseDirectory;
        private string LocalizationPath => Path.Combine(RealBaseDir, "Languages");

        public string CurrentLanguage => _currentLanguageCode;
        public bool IsRightToLeft => _currentCulture.TextInfo.IsRightToLeft;

        public LocalizationService()
        {
            _instance = this;
            _currentCulture = CultureInfo.CurrentUICulture;
            EnsureLanguageFilesExistLocally();
            LoadDefaultLanguage();
            LoadLanguage("en-us");
        }

        #region Initialization & Extraction
        private void EnsureLanguageFilesExistLocally()
        {
            try
            {
                if (!Directory.Exists(LocalizationPath)) Directory.CreateDirectory(LocalizationPath);

                var assembly = Assembly.GetExecutingAssembly();
                string resourcePrefix = "EvolveOS_Optimizer.Languages.";

                foreach (string resourceName in assembly.GetManifestResourceNames())
                {
                    if (resourceName.StartsWith(resourcePrefix) && resourceName.EndsWith(".xaml"))
                    {
                        string fileName = resourceName.Substring(resourcePrefix.Length);
                        string filePath = Path.Combine(LocalizationPath, fileName);

                        using var resourceStream = assembly.GetManifestResourceStream(resourceName);
                        if (resourceStream != null)
                        {
                            if (!File.Exists(filePath) || new FileInfo(filePath).Length != resourceStream.Length)
                            {
                                using var fileStream = File.Create(filePath);
                                resourceStream.CopyTo(fileStream);
                                Debug.WriteLine($"[Localization] Successfully extracted/updated {fileName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Localization] Failed to extract files: {ex.Message}");
            }
        }
        #endregion

        #region String Retrieval
        public string GetString(string key) => GetWithStatus(key).Value;

        public string this[string key] => GetString(key);

        public string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            try { return string.Format(format, args); }
            catch { return format; }
        }

        public (string Value, StringStatus Status) GetWithStatus(string key)
        {
            if (string.IsNullOrEmpty(key)) return (string.Empty, StringStatus.Found);

            if (_targetCache.TryGetValue(key, out var targetValue))
                return (targetValue, StringStatus.Found);

            if (_defaultCache.TryGetValue(key, out var defaultValue))
            {
                LogMissingString(key, defaultValue);
                return (defaultValue, StringStatus.Fallback);
            }

            LogMissingString(key, "");
            return (key, StringStatus.Missing);
        }
        #endregion

        #region Language Management
        public bool SetLanguage(string languageCode)
        {
            try
            {
                LoadLanguage(languageCode);
                return true;
            }
            catch { return false; }
        }

        public void LoadLanguage(string langCode)
        {
            _currentLanguage = langCode.ToLower();
            _currentLanguageCode = langCode;

            try { _currentCulture = new CultureInfo(langCode); }
            catch { _currentCulture = CultureInfo.InvariantCulture; }

            string filePath = Path.Combine(LocalizationPath, $"{_currentLanguage}.xaml");

            _targetCache.Clear();
            LoadDictionary(filePath, _targetCache);

            LanguageChanged?.Invoke(this, EventArgs.Empty);
            Refresh();
        }

        private void LoadDefaultLanguage()
        {
            string filePath = Path.Combine(LocalizationPath, "en-us.xaml");
            LoadDictionary(filePath, _defaultCache);
        }

        private void LoadDictionary(string filePath, Dictionary<string, string> cache)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                var doc = XDocument.Load(filePath);
                XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

                foreach (var element in doc.Descendants())
                {
                    var keyAttr = element.Attribute(x + "Key") ?? element.Attribute("Key");
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
        #endregion

        #region Logging & UI
        private void LogMissingString(string key, string fallbackValue)
        {
            if (!LocalMachineSettingsEngine.IsDeveloperMode || _currentLanguage == "en-us") return;

            lock (_logLock)
            {
                if (!_missingStringsLog.ContainsKey(key))
                {
                    _missingStringsLog[key] = string.IsNullOrEmpty(fallbackValue) ? "NEEDS_TRANSLATION" : fallbackValue;
                    try
                    {
                        string jsonPath = Path.Combine(LocalizationPath, $"MissingStrings_{_currentLanguage}.json");
                        File.WriteAllText(jsonPath, JsonSerializer.Serialize(_missingStringsLog, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch { }
                }
            }
        }

        public void Refresh() => OnPropertyChanged("Item[]");

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}