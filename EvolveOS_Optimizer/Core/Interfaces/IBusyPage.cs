// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IBusyPage
{
    bool IsBusy { get; }
    string BusyTitle { get; }
    string BusyMessage { get; }
    Task CancelWorkAsync();
}