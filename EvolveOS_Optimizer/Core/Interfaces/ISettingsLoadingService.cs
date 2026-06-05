// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISettingsLoadingService
{
    Task<ObservableCollection<SettingItemViewModel>> LoadConfiguredSettingsAsync(
        string featureModuleId,
        string progressMessage,
        ISettingsFeatureViewModel? parentViewModel = null);
    Task<Dictionary<string, SettingStateResult>> RefreshSettingStatesAsync(
        IEnumerable<SettingItemViewModel> settings);
}
