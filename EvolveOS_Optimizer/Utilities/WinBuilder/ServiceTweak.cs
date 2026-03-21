// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

namespace EvolveOS_Optimizer.Utilities.WinBuilder
{
    public class ServiceTweak
    {
        public string ServiceName { get; set; } = string.Empty;
        public string StartupType { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}
