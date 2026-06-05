// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Model
{
    public class ExcludeKeyEntry
    {
        public ExcludeType Type { get; set; }

        public string Path { get; set; } = "";

        public string? Pattern { get; set; }

        public static ExcludeKeyEntry Parse(string value)
        {
            var parts = value.Split('|');
            var entry = new ExcludeKeyEntry();

            if (parts.Length > 0)
            {
                entry.Type = parts[0].Trim().ToUpperInvariant() switch
                {
                    "FILE" => ExcludeType.File,
                    "PATH" => ExcludeType.Path,
                    "REG" => ExcludeType.Reg,
                    _ => ExcludeType.File
                };
            }
            if (parts.Length > 1) entry.Path = parts[1].Trim();
            if (parts.Length > 2) entry.Pattern = parts[2].Trim();

            return entry;
        }
    }
}
