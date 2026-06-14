// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Utilities.WinBuilder;

namespace EvolveOS_Optimizer.Core.Model
{
    public class WizardConfig
    {
        public string? Mode { get; set; } // "ISO" or "XML"
        public List<RegistryTweak> Tweaks { get; set; } = new();
    }
}