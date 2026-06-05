// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Utilities.Services;

public class ConfigImportState : IConfigImportState
{
    public bool IsActive { get; set; }
}
