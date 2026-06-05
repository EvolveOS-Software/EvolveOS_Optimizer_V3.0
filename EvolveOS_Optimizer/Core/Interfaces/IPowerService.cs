// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IPowerService
{
    Task<PowerPlan?> GetActivePowerPlanAsync();
    Task<IEnumerable<object>> GetAvailablePowerPlansAsync();
    Task<bool> DeletePowerPlanAsync(string powerPlanGuid);
}
