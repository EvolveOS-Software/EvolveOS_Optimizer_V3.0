// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class MonitorPositionViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string DisplayName { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string WallpaperPath { get; set; } = string.Empty;

        private string _position = "Bottom";
        public string Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    SetProperty(ref _position, value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLeft)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTop)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRight)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBottom)));
                }
            }
        }

        public bool IsLeft { get => Position == "Left"; set { if (value) Position = "Left"; } }
        public bool IsTop { get => Position == "Top"; set { if (value) Position = "Top"; } }
        public bool IsRight { get => Position == "Right"; set { if (value) Position = "Right"; } }
        public bool IsBottom { get => Position == "Bottom"; set { if (value) Position = "Bottom"; } }
    }

    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    public class DesktopWallpaperClass { }

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, out Windows.Foundation.Rect displayRect);
    }
}