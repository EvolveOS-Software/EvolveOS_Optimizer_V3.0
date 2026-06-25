// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISettingsFeatureViewModel : INotifyPropertyChanged, IDisposable
{
    string ModuleId { get; }
    string DisplayName { get; }
    ObservableCollection<SettingItemViewModel> Settings { get; }
    bool HasVisibleSettings { get; }
    bool IsExpanded { get; set; }
    bool IsLoading { get; }
    int SettingsCount { get; }
    string GroupDescriptionText { get; }
    ObservableCollection<SettingsGroup> GroupedSettings { get; }
    Task LoadSettingsAsync();
    Task RefreshSettingsAsync();
    Task RefreshSettingStatesAsync();
    void ApplySearchFilter(string searchText);

}
