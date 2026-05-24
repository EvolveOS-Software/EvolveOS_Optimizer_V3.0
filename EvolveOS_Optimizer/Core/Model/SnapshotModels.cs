// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Vanara.PInvoke;

namespace EvolveOS_Optimizer.Core.Model
{
    public enum ChangeType
    {
        Added,
        Deleted,
        Modified
    }

    public class RegistryChange
    {
        public ChangeType Type { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }

    public class RegistrySnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public HKEY RootHive { get; set; }
        public string BasePath { get; set; } = string.Empty;

        public Dictionary<string, ulong> Keys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}