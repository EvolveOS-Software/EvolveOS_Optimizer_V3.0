// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Vanara.PInvoke;

namespace EvolveOS_Optimizer.Core.Model
{
    public class RegistryNavigationMessage
    {
        #region Properties
        public HKEY RootHive { get; }
        public string Path { get; }
        public string? TargetValueName { get; }
        #endregion

        #region Constructor
        public RegistryNavigationMessage(HKEY rootHive, string path, string? targetValueName = null)
        {
            RootHive = rootHive;
            Path = path;
            TargetValueName = targetValueName;
        }
        #endregion
    }
}