// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class SnapshotItemViewModel : ObservableObject
{
    public string Id { get; }
    public string DisplayText { get; }

    private readonly Action<SnapshotItemViewModel> _deleteAction;

    public SnapshotItemViewModel(string id, string displayText, Action<SnapshotItemViewModel> deleteAction)
    {
        Id = id;
        DisplayText = displayText;
        _deleteAction = deleteAction;
    }

    [RelayCommand]
    private void Delete()
    {
        _deleteAction?.Invoke(this);
    }
}