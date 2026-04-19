// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class TweakBackupModel
    {
        public Dictionary<string, object> System { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> Interface { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> Privacy { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> Services { get; set; } = new Dictionary<string, object>();
    }
}
