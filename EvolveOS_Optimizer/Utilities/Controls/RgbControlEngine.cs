// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO; // 🚀 NEW: Required for saving the JSON file
using System.Linq;
using System.Text.Json; // 🚀 NEW: Required for JSON Serialization
using System.Threading.Tasks;
using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Helpers;
using RGB.NET.Core;
using RGB.NET.Devices.Asus;
using RGB.NET.Devices.CoolerMaster;
using RGB.NET.Devices.Corsair;
using RGB.NET.Devices.Logitech;
using RGB.NET.Devices.Msi;
using RGB.NET.Devices.Razer;
using RGB.NET.Devices.SteelSeries;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using Windows.Devices.Lights.Effects;
using Windows.UI;

public class RgbControlEngine : IRgbControlEngine
{
    public static RgbControlEngine Instance { get; } = new RgbControlEngine();

    private bool _isInitialized = false;

    private readonly List<RgbDeviceInfo> _devices = new();
    public IReadOnlyList<RgbDeviceInfo> Devices => _devices.AsReadOnly();
    public bool IsConnected => _devices.Any();

    // 🚀 BROUGHT NATIVE BACK
    private readonly Dictionary<string, LampArray> _nativeDevices = new();
    private readonly Dictionary<string, LampArrayEffectPlaylist> _activePlaylists = new();

    private readonly RGBSurface _rgbSurface = new RGBSurface();
    private readonly Dictionary<string, IRGBDevice> _rgbNetDevices = new();
    private bool _isRgbNetInitialized = false;

    private System.Timers.Timer? _heartbeatTimer;

    private RgbControlEngine() { }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        LoadColorsFromFile(); // 🚀 NEW: Load colors from disk first!

        _devices.Clear();
        _nativeDevices.Clear();
        _rgbNetDevices.Clear();

        // 1. 🚀 PROPER ASYNC RESTORE
        await RestoreWindowsDynamicLightingAsync().ConfigureAwait(false);

        // 2. 🚀 PROPER NON-BLOCKING INITIALIZATION
        await InitializeNativeLightingAsync().ConfigureAwait(false);

        // Push the heavy RGB.NET loading to its own safe background execution
        await Task.Run(() => InitializeRgbNetLighting()).ConfigureAwait(false);

        if (_heartbeatTimer == null)
        {
            _heartbeatTimer = new System.Timers.Timer(100);
            _heartbeatTimer.Elapsed += (sender, args) =>
            {
                if (_isRgbNetInitialized)
                {
                    try { _rgbSurface.Update(); } catch { }
                }
            };
            _heartbeatTimer.Start();
        }

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

            // 🚀 THE FIX: Asynchronously wait instead of freezing the thread
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

            // 🚀 THE FIX: Add AsTask().ConfigureAwait(false) to prevent WinRT UI thread deadlocks
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

    #region RGB.NET Initialization
    private void InitializeRgbNetLighting()
    {
        try
        {
            if (!_isRgbNetInitialized)
            {
                try { _rgbSurface.Load(AsusDeviceProvider.Instance); } catch { }
                try { _rgbSurface.Load(MsiDeviceProvider.Instance); } catch { }
                try { _rgbSurface.Load(CorsairDeviceProvider.Instance); } catch { }
                try { _rgbSurface.Load(CoolerMasterDeviceProvider.Instance); } catch { }
                try { _rgbSurface.Load(RazerDeviceProvider.Instance); } catch { }
                try { _rgbSurface.Load(LogitechDeviceProvider.Instance); } catch { }
                try { _rgbSurface.Load(SteelSeriesDeviceProvider.Instance); } catch { }

                _rgbSurface.AlignDevices();
                _isRgbNetInitialized = true;
            }

            int index = 0;
            foreach (var device in _rgbSurface.Devices)
            {
                string id = $"RgbNet_{index++}";
                _rgbNetDevices[id] = device;

                _devices.Add(new RgbDeviceInfo
                {
                    Id = id,
                    Name = $"{device.DeviceInfo.DeviceName} (RGB.NET)",
                    LedCount = device.Count(),
                    IsNative = false
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RGB.NET] Master initialization failed: {ex.Message}");
        }
    }
    #endregion

    #region Color Routing & Storage

    // 🚀 NEW: Store colors locally in memory
    private Dictionary<string, string> _colorCache = new();

    // 🚀 NEW: Save to the exact same safe folder you use for other EvolveOS Optimizer files
    private readonly string _colorSettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvolveOS_Optimizer", "RgbColors.json");

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
                // Parse the saved Hex string back to a Color object
                byte a = Convert.ToByte(hexColor.Substring(0, 2), 16);
                byte r = Convert.ToByte(hexColor.Substring(2, 2), 16);
                byte g = Convert.ToByte(hexColor.Substring(4, 2), 16);
                byte b = Convert.ToByte(hexColor.Substring(6, 2), 16);

                return Windows.UI.Color.FromArgb(a, r, g, b);
            }
        }
        catch { }

        // Fallback Evolve Blue
        return Windows.UI.Color.FromArgb(255, 0, 120, 215);
    }

    private void SaveColorToSettings(string deviceId, Windows.UI.Color color)
    {
        try
        {
            // Convert the color to a safe Hex string (e.g., "FFD70000")
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
        else if (deviceId.StartsWith("RgbNet_") && _rgbNetDevices.TryGetValue(deviceId, out var rgbDevice))
        {
            SetRgbNetColor(rgbDevice, color);
        }

        await Task.CompletedTask;
    }

    public async Task SetAllColorsAsync(Windows.UI.Color color)
    {
        foreach (var kvp in _nativeDevices)
        {
            SaveColorToSettings(kvp.Key, color);
            SetNativeColor(kvp.Key, kvp.Value, color);
        }

        foreach (var kvp in _rgbNetDevices)
        {
            SaveColorToSettings(kvp.Key, color);
            SetRgbNetColor(kvp.Value, color);
        }

        if (_isRgbNetInitialized) _rgbSurface.Update();

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
                // 🚀 THE REAL FIX: 1 Second duration. 
                // When you minimize, Windows LMS takes over and safely loops this 1-second effect forever.
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

    private void SetRgbNetColor(IRGBDevice device, Windows.UI.Color color)
    {
        try
        {
            var rgbNetColor = new RGB.NET.Core.Color(color.A, color.R, color.G, color.B);

            foreach (var led in device)
            {
                led.Color = rgbNetColor;
            }

            _rgbSurface.Update();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RGB.NET] Failed to set color for {device.DeviceInfo.DeviceName}: {ex.Message}");
        }
    }
    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_heartbeatTimer != null)
        {
            _heartbeatTimer.Stop();
            _heartbeatTimer.Dispose();
            _heartbeatTimer = null;
        }

        foreach (var playlist in _activePlaylists.Values)
        {
            try { playlist.Stop(); } catch { }
        }
        _activePlaylists.Clear();
        _nativeDevices.Clear();

        if (_isRgbNetInitialized)
        {
            try { _rgbSurface.Dispose(); } catch { }
            _isRgbNetInitialized = false;
        }
        _rgbNetDevices.Clear();
        _devices.Clear();

        _isInitialized = false;

        await Task.CompletedTask;
    }
}