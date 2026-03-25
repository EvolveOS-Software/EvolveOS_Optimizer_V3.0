// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

namespace EvolveOS_Optimizer.Utilities.WinBuilder
{
    public class IsoBuildOptions
    {
        // Paths
        public string SourceIsoPath { get; set; } = string.Empty;
        public string OutputIsoPath { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;

        // Edition and Format Selectors
        public string TargetEdition { get; set; } = "Pro";
        public string ImageFormat { get; set; } = "WIM";

        // Core Windows Setup Bypasses
        public bool BypassWin11Requirements { get; set; }
        public bool BypassMicrosoftAccount { get; set; }
        public bool EnableNet35 { get; set; }

        // Specific Core Tweaks
        public bool DisableHibernate { get; set; }
        public bool AlignTaskbarLeft { get; set; }
        public bool ForceDarkMode { get; set; }

        // Deep System App Removal
        public bool RemoveMicrosoftEdge { get; set; }
        public bool RemoveOneDrive { get; set; }

        // Selectable Customizations
        public List<string> AppsToRemove { get; set; } = new List<string>();
        public List<RegistryTweak> RegistryTweaks { get; set; } = new List<RegistryTweak>();
        public List<ServiceTweak> ServiceTweaks { get; set; } = new List<ServiceTweak>();
        public List<string> ElementsToRemove { get; set; } = new List<string>();
    }
}