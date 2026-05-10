// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class CleanerEntry
    {
        public string Name { get; set; } = "";
        public string? Section { get; set; }
        public int? LangSecRef { get; set; }
        public List<string> DetectKeys { get; set; } = new();
        public List<string> DetectFiles { get; set; } = new();
        public string? SpecialDetect { get; set; }
        public List<FileKeyEntry> FileKeys { get; set; } = new();
        public List<RegKeyEntry> RegKeys { get; set; } = new();
        public List<ExcludeKeyEntry> ExcludeKeys { get; set; } = new();
        public string? Warning { get; set; }
        public bool Default { get; set; } = true;
    }
}
