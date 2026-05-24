// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class RegistryItemModel
    {
        #region Properties
        public string KeyPath { get; set; } = "";
        public string? ValueName { get; set; }
        public bool IsDeleteKey => ValueName == null;
        #endregion

        #region Overrides
        public override string ToString() => ValueName != null ? $"{KeyPath} → {ValueName}" : KeyPath;
        #endregion
    }
}
