// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public class TelemetryDataPayload
{
    public double Cpu { get; set; }
    public double Ram { get; set; }
    public double Gpu { get; set; }
    public double NetDown { get; set; }
    public double NetUp { get; set; }
    public string ProcCount { get; set; } = "0";
    public string SvcCount { get; set; } = "0";
    public bool IsFullSecond { get; set; }
}