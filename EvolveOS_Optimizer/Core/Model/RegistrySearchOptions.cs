// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class RegistrySearchOptions
    {
        #region Properties
        public string Query { get; set; } = string.Empty;
        public bool SearchKeys { get; set; } = true;
        public bool SearchValues { get; set; } = true;
        public bool SearchData { get; set; } = true;
        public bool MatchWholeString { get; set; } = false;
        #endregion
    }
}
