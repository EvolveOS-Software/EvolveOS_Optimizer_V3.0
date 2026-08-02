// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI;

namespace EvolveOS_Optimizer.Core.Interfaces
{
    public class RgbDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int LedCount { get; set; }
        public bool IsNative { get; set; }
    }

    public interface IRgbControlEngine : IAsyncDisposable
    {
        bool IsConnected { get; }
        IReadOnlyList<RgbDeviceInfo> Devices { get; }

        Task InitializeAsync();
        Task SetDeviceColorAsync(string deviceId, Color color);
        Task SetAllColorsAsync(Color color);

        // 🚀 NEW: Method to retrieve the saved color
        Color GetSavedColor(string deviceId);
    }
}