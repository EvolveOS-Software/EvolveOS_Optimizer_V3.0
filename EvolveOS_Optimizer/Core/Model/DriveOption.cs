// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class DriveOption
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string DisplayName => Path == "ALL" ? Name : $"{Name} ({Path})";
    }
}