// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class RegKeyEntry
    {
        public string KeyPath { get; set; } = "";

        public string? ValueName { get; set; }

        public static RegKeyEntry Parse(string value)
        {
            var idx = value.IndexOf('|');
            if (idx >= 0)
            {
                return new RegKeyEntry
                {
                    KeyPath = value[..idx].Trim(),
                    ValueName = value[(idx + 1)..].Trim()
                };
            }
            return new RegKeyEntry { KeyPath = value.Trim() };
        }
    }
}
