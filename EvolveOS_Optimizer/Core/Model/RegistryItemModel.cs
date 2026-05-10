// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class RegistryItemModel
    {
        public string KeyPath { get; set; } = "";
        public string? ValueName { get; set; }
        public bool IsDeleteKey => ValueName == null;

        public override string ToString() =>
            ValueName != null ? $"{KeyPath} → {ValueName}" : KeyPath;
    }
}
