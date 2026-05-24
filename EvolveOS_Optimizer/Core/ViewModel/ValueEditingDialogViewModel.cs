// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class ValueEditingDialogViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ValueItem? ValueItem { get; set; }

    public ValueEditingDialogViewModel()
    {
    }
}
