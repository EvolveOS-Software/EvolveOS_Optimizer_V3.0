// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class AppSettingsBackup
    {
        public Dictionary<string, object> CurrentUserSettings { get; set; } = new();
        public Dictionary<string, object> LocalMachineSettings { get; set; } = new();
    }
}
