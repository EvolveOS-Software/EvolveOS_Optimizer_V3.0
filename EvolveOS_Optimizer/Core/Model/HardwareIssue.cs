// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License. 

using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.Model
{
    public class HardwareIssue
    {
        public int WmiErrorCode { get; set; } = 0;
        public string ErrorCodeHex => WmiErrorCode == 0 ? "0x00" : $"0x{WmiErrorCode:X}";

        public string? DeviceName { get; set; }
        public string? DeviceId { get; set; }
        public string? ComponentDisplayName { get; set; }
        public string? HardwareType { get; set; }
        public string? IssueSummary { get; set; }
        public string? RecommendedFix { get; set; }

        public bool IsFixable { get; set; } = false;

        public Visibility FixButtonVisibility =>
            IsFixable ? Visibility.Visible : Visibility.Collapsed;

        public string StatusGlyph => MapGlyph(WmiErrorCode);
        public string ErrorCodeDescription => MapWmiCodeToDescription(WmiErrorCode);
        public SolidColorBrush StatusBrush => MapBrush(WmiErrorCode);

        private string MapGlyph(int code)
        {
            if (code == 0) return "\uE73E";  // Green Check: Healthy
            if (code == 43) return "\uE711"; // Red X: Code 43 (Broken driver)
            if (code == 10) return "\uE7BA"; // Warning: Code 10 (Cannot start)
            if (code == 22) return "\uE946"; // Info Circle: Code 22 (Disabled)
            return "\uE783"; // General Info: Other error code
        }

        private SolidColorBrush MapBrush(int code)
        {
            if (code == 0) return new SolidColorBrush(Colors.LimeGreen);
            if (code == 43) return new SolidColorBrush(Colors.Red);
            if (code == 10) return new SolidColorBrush(Colors.Orange);
            if (code == 22) return new SolidColorBrush(Colors.SkyBlue);
            return new SolidColorBrush(Colors.LightGray);
        }

        private string MapWmiCodeToDescription(int code)
        {
            if (code == 0) return ResourceString.GetString("wmi_code_0_ok") ?? "Operational";
            if (code == 43) return ResourceString.GetString("wmi_code_43_desc") ?? "Code 43 (Driver Conflict)";
            if (code == 10) return ResourceString.GetString("wmi_code_10_desc") ?? "Code 10 (Failed Start)";
            if (code == 22) return "Code 22 (Device Disabled)";
            return $"Error Code: {code}";
        }
    }
}