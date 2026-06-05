// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IHardwareDetectionService
{
    Task<bool> HasBatteryAsync();
    Task<bool> HasLidAsync();
    Task<bool> SupportsBrightnessControlAsync();
    Task<bool> SupportsHybridSleepAsync();
}
