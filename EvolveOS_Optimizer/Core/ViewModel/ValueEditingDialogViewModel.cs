// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class ValueEditingDialogViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ValueItem? ValueItem { get; set; }

    public ValueEditingDialogViewModel()
    {
    }
}
