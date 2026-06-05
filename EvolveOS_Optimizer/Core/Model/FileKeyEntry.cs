// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Model
{
    public class FileKeyEntry
    {
        public string Path { get; set; } = "";

        public string Pattern { get; set; } = "*.*";

        public FileKeyFlag Flag { get; set; } = FileKeyFlag.None;

        public static FileKeyEntry Parse(string value)
        {
            var parts = value.Split('|');
            var entry = new FileKeyEntry { Path = parts[0].Trim() };

            if (parts.Length == 2)
            {
                var p = parts[1].Trim().ToUpperInvariant();
                if (p is "RECURSE" or "REMOVESELF")
                    entry.Flag = p == "RECURSE" ? FileKeyFlag.Recurse : FileKeyFlag.RemoveSelf;
                else
                    entry.Pattern = parts[1].Trim();
            }
            else if (parts.Length > 2)
            {
                entry.Pattern = parts[1].Trim();
                entry.Flag = parts[2].Trim().ToUpperInvariant() switch
                {
                    "RECURSE" => FileKeyFlag.Recurse,
                    "REMOVESELF" => FileKeyFlag.RemoveSelf,
                    _ => FileKeyFlag.None
                };
            }

            return entry;
        }
    }
}
