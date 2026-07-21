// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;

namespace EvolveOS_Optimizer.Core.Model;

public partial class FanCurvePoint : ObservableObject
{
    [ObservableProperty]
    public partial double Temperature { get; set; }

    [ObservableProperty]
    public partial double SpeedPercentage { get; set; }

    public FanCurvePoint(double temperature, double speedPercentage)
    {
        Temperature = temperature;
        SpeedPercentage = speedPercentage;
    }
}