// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

namespace EvolveOS_Optimizer.Utilities.WinBuilder
{
    public class RegistryTweak
    {
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RegCommand { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}