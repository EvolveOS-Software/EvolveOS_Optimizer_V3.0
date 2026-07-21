// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Model;

public partial class CoolingDevice : ObservableObject
{
    public string? Name { get; set; }
    public CoolingDeviceType Type { get; set; }

    [ObservableProperty]
    public partial int CurrentRpm { get; set; }

    public ObservableCollection<FanCurvePoint> CurvePoints { get; set; } = new();
}