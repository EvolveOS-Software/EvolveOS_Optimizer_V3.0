// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Helpers;

// 🚀 NOTICE: RGB.NET namespaces are completely removed!
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using Windows.Devices.Lights.Effects;

namespace EvolveOS_Optimizer.Utilities.Controls
{
    public class RgbControlEngine : IRgbControlEngine
    {
        public static RgbControlEngine Instance { get; } = new RgbControlEngine();

        private bool _isInitialized = false;

        private readonly List<RgbDeviceInfo> _devices = new();
        public IReadOnlyList<RgbDeviceInfo> Devices => _devices.AsReadOnly();
        public bool IsConnected => _devices.Any();

        // Native Windows Devices
        private readonly Dictionary<string, LampArray> _nativeDevices = new();
        private readonly Dictionary<string, LampArrayEffectPlaylist> _activePlaylists = new();

        // 🚀 NEW: Store colors locally in memory
        private Dictionary<string, string> _colorCache = new();
        private readonly string _colorSettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvolveOS_Optimizer", "RgbColors.json");

        private RgbControlEngine() { }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            LoadColorsFromFile();

            _devices.Clear();
            _nativeDevices.Clear();

            // 1. Initialize Windows Dynamic Lighting
            await RestoreWindowsDynamicLightingAsync().ConfigureAwait(false);
            await InitializeNativeLightingAsync().ConfigureAwait(false);

            // 2. 🚀 Initialize OpenRGB Native Proxy (Replaces RGB.NET entirely)
            await Task.Run(() =>
            {
                LightingBootstrapper.StartLightingSystem();
            }).ConfigureAwait(false);

            // Add a master virtual device to represent all third-party hardware handled by the DLL
            _devices.Add(new RgbDeviceInfo
            {
                Id = "OpenRGB_Global",
                Name = "Hardware RGB (Corsair, Asus, Razer, etc.)",
                LedCount = 0, // Managed directly by the OpenRGB Server
                IsNative = false
            });

            // 3. Restore all saved colors instantly
            foreach (var device in _devices)
            {
                var savedColor = GetSavedColor(device.Id);
                await SetDeviceColorAsync(device.Id, savedColor).ConfigureAwait(false);
            }

            _isInitialized = true;
        }

        private async Task RestoreWindowsDynamicLightingAsync()
        {
            try
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Lighting", "AmbientLightingEnabled", 1);
                await Task.Delay(500).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RGB Engine] Failed to restore Windows lighting: {ex.Message}");
            }
        }

        #region Native Windows Initialization
        private async Task InitializeNativeLightingAsync()
        {
            try
            {
                string selector = LampArray.GetDeviceSelector();
                var deviceInfos = await DeviceInformation.FindAllAsync(selector).AsTask().ConfigureAwait(false);

                foreach (var info in deviceInfos)
                {
                    var lampArray = await LampArray.FromIdAsync(info.Id).AsTask().ConfigureAwait(false);
                    if (lampArray != null)
                    {
                        string id = "Win_" + lampArray.DeviceId;
                        _nativeDevices[id] = lampArray;

                        _devices.Add(new RgbDeviceInfo
                        {
                            Id = id,
                            Name = $"{info.Name} (Native)",
                            LedCount = lampArray.LampCount,
                            IsNative = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dynamic Lighting] Initialization failed: {ex.Message}");
            }
        }
        #endregion

        #region Color Routing & Storage

        private void LoadColorsFromFile()
        {
            try
            {
                if (File.Exists(_colorSettingsFile))
                {
                    string json = File.ReadAllText(_colorSettingsFile);
                    _colorCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RGB Engine] Failed to load colors file: {ex.Message}");
            }
        }

        private void SaveColorsToFile()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_colorSettingsFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(_colorCache);
                File.WriteAllText(_colorSettingsFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RGB Engine] Failed to save colors file: {ex.Message}");
            }
        }

        public Windows.UI.Color GetSavedColor(string deviceId)
        {
            try
            {
                if (_colorCache.Count == 0) LoadColorsFromFile();

                if (_colorCache.TryGetValue(deviceId, out string? hexColor) && !string.IsNullOrEmpty(hexColor))
                {
                    byte a = Convert.ToByte(hexColor.Substring(0, 2), 16);
                    byte r = Convert.ToByte(hexColor.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hexColor.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hexColor.Substring(6, 2), 16);

                    return Windows.UI.Color.FromArgb(a, r, g, b);
                }
            }
            catch { }

            return Windows.UI.Color.FromArgb(255, 0, 120, 215);
        }

        private void SaveColorToSettings(string deviceId, Windows.UI.Color color)
        {
            try
            {
                string hex = $"{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                _colorCache[deviceId] = hex;
                SaveColorsToFile();
            }
            catch { }
        }

        public async Task SetDeviceColorAsync(string deviceId, Windows.UI.Color color)
        {
            SaveColorToSettings(deviceId, color);

            if (deviceId.StartsWith("Win_") && _nativeDevices.TryGetValue(deviceId, out var lampArray))
            {
                SetNativeColor(deviceId, lampArray, color);
            }
            else if (deviceId == "OpenRGB_Global")
            {
                // 🚀 Send colors to Corsair, Asus, etc. via our C++ DLL
                LightingNativeBridge.SetGlobalColor(color.R, color.G, color.B);
            }

            await Task.CompletedTask;
        }

        public async Task SetAllColorsAsync(Windows.UI.Color color)
        {
            // 1. Update Windows Native Devices
            foreach (var kvp in _nativeDevices)
            {
                SaveColorToSettings(kvp.Key, color);
                SetNativeColor(kvp.Key, kvp.Value, color);
            }

            // 2. Update Third-Party Hardware via DLL
            SaveColorToSettings("OpenRGB_Global", color);
            LightingNativeBridge.SetGlobalColor(color.R, color.G, color.B);

            await Task.CompletedTask;
        }

        private void SetNativeColor(string deviceId, LampArray targetDevice, Windows.UI.Color color)
        {
            try
            {
                if (targetDevice == null || targetDevice.LampCount == 0) return;

                if (_activePlaylists.TryGetValue(deviceId, out var existingPlaylist))
                {
                    existingPlaylist.Stop();
                }

                var playlist = new LampArrayEffectPlaylist
                {
                    RepetitionMode = LampArrayRepetitionMode.Forever
                };

                int[] indices = new int[targetDevice.LampCount];
                for (int i = 0; i < targetDevice.LampCount; i++) indices[i] = i;

                var effect = new LampArraySolidEffect(targetDevice, indices)
                {
                    Color = color,
                    Duration = TimeSpan.FromSeconds(1)
                };

                playlist.Append(effect);
                _activePlaylists[deviceId] = playlist;

                playlist.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dynamic Lighting] Failed to set color for {deviceId}: {ex.Message}");
            }
        }
        #endregion

        public async ValueTask DisposeAsync()
        {
            foreach (var playlist in _activePlaylists.Values)
            {
                try { playlist.Stop(); } catch { }
            }
            _activePlaylists.Clear();
            _nativeDevices.Clear();
            _devices.Clear();

            // 🚀 Tell our C++ DLL to sever the connection gracefully
            LightingNativeBridge.ShutdownLighting();

            _isInitialized = false;

            await Task.CompletedTask;
        }
    }
}