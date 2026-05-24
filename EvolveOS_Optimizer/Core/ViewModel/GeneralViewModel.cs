// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Model;
using Microsoft.UI.Dispatching;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class GeneralViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    public partial KeyItem? KeyItem { get; set; }

    [ObservableProperty]
    public partial string FormattedCreatedAt { get; set; } = string.Empty;

    public GeneralViewModel(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    partial void OnKeyItemChanged(KeyItem? value)
    {
        if (value == null) return;

        if (value.CreatedAt == DateTime.MinValue)
        {
            FormattedCreatedAt = "Date not available";
        }
        else
        {
            FormattedCreatedAt = value.CreatedAt.ToString("f");
        }
    }
}