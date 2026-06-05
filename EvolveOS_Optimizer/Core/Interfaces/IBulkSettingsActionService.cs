// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IBulkSettingsActionService
{
    Task<int> ApplyRecommendedAsync(IEnumerable<string> settingIds, IProgress<TaskProgressDetail>? progress = null);
    Task<int> ResetToDefaultsAsync(IEnumerable<string> settingIds, IProgress<TaskProgressDetail>? progress = null);
    Task<int> GetAffectedCountAsync(IEnumerable<string> settingIds, BulkActionType actionType);
}
