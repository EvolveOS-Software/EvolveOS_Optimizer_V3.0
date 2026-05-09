// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License. 

namespace EvolveOS_Optimizer.Core.Model
{
    public class DnsBenchmarkingItem
    {
        public string Name { get; set; } = string.Empty;

        public string IP { get; set; } = string.Empty;

        public long Latency { get; set; } = -1;

        public Color LatencyColor { get; set; }

        public DnsPreset? PresetReference { get; set; }

        public string LatencyStr => Latency >= 0 ? $"{Latency} ms" : "Timeout";
    }
}