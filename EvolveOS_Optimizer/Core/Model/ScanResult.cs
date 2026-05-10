// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Utilities.Extensions;

namespace EvolveOS_Optimizer.Core.Model
{
    public class ScanResult
    {
        public CleanerEntry Entry { get; set; } = null!;
        public List<string> FilesToDelete { get; set; } = new();
        public List<RegistryItemModel> RegistryToDelete { get; set; } = new();
        public long TotalBytes { get; set; }
        public string FormattedSize => TotalBytes.FormatBytes();
    }
}
