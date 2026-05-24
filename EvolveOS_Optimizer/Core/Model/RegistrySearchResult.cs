// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Vanara.PInvoke;

namespace EvolveOS_Optimizer.Core.Model
{
    public class RegistrySearchResult
    {
        #region Properties
        public HKEY RootHive { get; set; }

        public string FullPath { get; set; } = string.Empty;

        public string MatchType { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Data { get; set; } = string.Empty;
        #endregion
    }
}