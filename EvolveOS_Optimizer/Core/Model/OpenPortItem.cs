// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;

namespace EvolveOS_Optimizer.Core.Model
{
    public class OpenPortItem : ObservableObject
    {
        public string Protocol { get; set; } = string.Empty;
        public string LocalIP { get; set; } = string.Empty;
        public int Port { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;

        public bool IsExposed { get; set; }

        public string StatusText => IsExposed ? "Exposed" : "Local Only";

        public Color StatusColor => IsExposed ? Colors.Orange : Colors.LimeGreen;

        public string ProcessPath { get; set; } = string.Empty;
        public bool IsVerified { get; set; }

        public string RiskLevel { get; set; } = "Low";

        public Color RiskColor { get; set; } = Colors.LimeGreen;

        public string Description { get; set; } = string.Empty;
        public string FirewallStatus { get; set; } = "Unknown";
    }
}