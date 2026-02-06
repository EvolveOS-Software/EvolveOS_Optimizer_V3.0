using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            ["Backdrop"] = "Mica"
        };

        private static readonly Dictionary<string, object> _cachedSettings = new Dictionary<string, object>(_defaultSettings);

        internal static string Backdrop { get => (string)_cachedSettings["Backdrop"]; set => ChangingParameters("Backdrop", value); }



        private static void ChangingParameters(string key, object value)
        {
            _cachedSettings[key] = value;

            try
            {
                // Use CreateSubKey to ensure the path exists
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

                        regKey.Flush(); // <--- Force the OS to write now
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
            if (App.Current is not App currentApp || currentApp.MainWindow is not MainWindow targetWindow) return;

            if (key == "Backdrop")
            {
                // Ensure this method in MainWindow handles "MicaAlt" and "Acrylic" strings
                targetWindow.SetBackdropByName(value.ToString() ?? "None");
            }
        }

        internal static void CheckingParameters()
        {
            // 1. Ensure the directory exists
            try
            {
                using (RegistryKey? rootKey = Registry.CurrentUser.OpenSubKey(PathLocator.Registry.SubKey, false))
                {
                    foreach (var kv in _defaultSettings)
                    {
                        // 2. If the key is missing or the specific value is missing
                        if (rootKey == null || rootKey.GetValue(kv.Key) == null)
                        {
                            Debug.WriteLine($"[Settings] {kv.Key} not found. Saving default...");
                            ChangingParameters(kv.Key, kv.Value);
                        }
                        else
                        {
                            object rawVal = rootKey.GetValue(kv.Key)!;
                            _cachedSettings[kv.Key] = kv.Value switch
                            {
                                bool => Convert.ToInt32(rawVal) != 0,
                                int => Convert.ToInt32(rawVal),
                                _ => rawVal.ToString() ?? kv.Value.ToString()!
                            };
                            Debug.WriteLine($"[Settings] Loaded {kv.Key}: {_cachedSettings[kv.Key]}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] CheckingParameters Error: {ex.Message}");
            }

            UpdateAppInstance();
        }

        private static void UpdateAppInstance()
        {
            if (App.Current is not App currentApp) return;

            // Update Backdrop via MainWindow helper
            if (currentApp.MainWindow is MainWindow target)
            {
                target.SetBackdropByName(Backdrop);
            }
        }
    }
}