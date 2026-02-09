using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Win32;
using System.Reflection;

namespace EvolveOS_Optimizer.Utilities.Controls
{
    internal sealed class SettingsEngine
    {
        public static readonly string[] AvailableBackdrops = { "None", "Mica", "MicaAlt", "Acrylic" };

        internal static string currentRelease = (Assembly.GetEntryAssembly() ?? throw new InvalidOperationException())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split(' ').Last().Trim() ?? "1.0.0";

        internal static readonly string currentName = AppDomain.CurrentDomain.FriendlyName;
        internal static readonly string currentLocation = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

        private const string AppName = "EvolveOS Optimizer";
        private const string ScheduledTaskName = "[EvolveOS Optimizer]";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static readonly Dictionary<string, object> _defaultSettings = new Dictionary<string, object>
        {
            ["Backdrop"] = "Mica",
            ["AccentColor"] = "#FF0078D4",
            ["Language"] = "en-us"
        };

        private static readonly Dictionary<string, object> _cachedSettings = new Dictionary<string, object>(_defaultSettings);

        internal static string Backdrop { get => (string)_cachedSettings["Backdrop"]; set => ChangingParameters("Backdrop", value); }
        internal static string AccentColor { get => (string)_cachedSettings["AccentColor"]; set => ChangingParameters("AccentColor", value); }
        internal static string Language { get => (string)_cachedSettings["Language"]; set => ChangingParameters("Language", value); }

        private static void ChangingParameters(string key, object value)
        {
            _cachedSettings[key] = value;

            try
            {
                using (RegistryKey? regKey = Registry.CurrentUser.CreateSubKey(PathLocator.Registry.SubKey, true))
                {
                    if (regKey != null)
                    {
                        if (value is bool b)
                            regKey.SetValue(key, b ? 1 : 0, RegistryValueKind.DWord);
                        else if (value is int i)
                            regKey.SetValue(key, i, RegistryValueKind.DWord);
                        else
                            regKey.SetValue(key, value.ToString() ?? "", RegistryValueKind.String);

                        regKey.Flush();
                        Debug.WriteLine($"[Settings] SAVED TO: HKCU\\{PathLocator.Registry.SubKey}\\{key} = {value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] WRITE ERROR: {ex.Message}");
            }

            ApplyLiveSettings(key, value);
        }

        private static void ApplyLiveSettings(string key, object value)
        {
            if (App.Current is not App currentApp) return;

            if (key == "Language")
            {
                SetAppLanguage(value.ToString() ?? "en-us");
            }

            if (currentApp.MainWindow is MainWindow mainWindow)
            {
                if (key == "Backdrop")
                {
                    UIHelper.ApplyBackdrop(mainWindow, value.ToString() ?? "None");
                }
                else if (key == "AccentColor")
                {
                    MainWindow.ApplyAccentColor(value.ToString() ?? "#FF0078D4");
                }
            }
        }

        internal static void CheckingParameters()
        {
            try
            {
                using (RegistryKey? rootKey = Registry.CurrentUser.OpenSubKey(PathLocator.Registry.SubKey, false))
                {
                    foreach (var kv in _defaultSettings)
                    {
                        if (rootKey != null && rootKey.GetValue(kv.Key) != null)
                        {
                            object rawVal = rootKey.GetValue(kv.Key)!;
                            _cachedSettings[kv.Key] = kv.Value switch
                            {
                                bool => Convert.ToInt32(rawVal) != 0,
                                int => Convert.ToInt32(rawVal),
                                _ => rawVal.ToString() ?? kv.Value.ToString()!
                            };
                        }
                    }
                }

                string startLang = Language;
                SetAppLanguage(startLang);
            }
            catch (Exception ex) { Debug.WriteLine($"[Settings] CheckingParameters Error: {ex.Message}"); }

            UpdateAppInstance();
        }

        public static void SetAppLanguage(string langCode)
        {
            string safeCode = langCode.ToLower().Trim();
            if (safeCode == "en") safeCode = "en-us";
            if (safeCode == "fr") safeCode = "fr-fr";
            if (safeCode == "nl") safeCode = "nl-nl";

            LocalizationService.Instance.LoadLanguage(safeCode);

            Debug.WriteLine($"[Settings] Language logic completed via C# Cache for: {safeCode}");
        }

        private static void UpdateAppInstance()
        {
            if (App.Current is not App currentApp) return;

            if (currentApp.MainWindow is MainWindow target)
            {
                target.SetBackdropByName(Backdrop);
            }
        }
    }
}