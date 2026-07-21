// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using Microsoft.UI.Dispatching;

namespace EvolveOS_Optimizer.Utilities.Services;

public class FanControlEngine
{
    public static FanControlEngine Instance { get; } = new FanControlEngine();

    public ObservableCollection<FanControlViewModel> AllFans { get; } = new();
    public ObservableCollection<FanControlViewModel> CpuFans { get; } = new();
    public ObservableCollection<FanControlViewModel> GpuFans { get; } = new();
    public ObservableCollection<FanControlViewModel> CaseFans { get; } = new();
    public ObservableCollection<FanControlViewModel> WaterPumps { get; } = new();

    private bool _isInitialized = false;
    private volatile bool _isShuttingDown = false;
    private DispatcherQueue? _dispatcherQueue;

    public void Initialize()
    {
        if (_isInitialized) return;
        _isShuttingDown = false;

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        var service = HardwareTemperatureService.Instance;
        service.Initialize();
        service.UpdateSensors();

        var controlSensors = service.GetFanControlSensors();

        foreach (var control in controlSensors)
        {
            var rpmSensor = service.GetMatchingRpmSensor(control);
            if (rpmSensor == null || !rpmSensor.Value.HasValue || rpmSensor.Value <= 0) continue;

            var fanVm = new FanControlViewModel(control, rpmSensor);
            fanVm.LoadPreferences();

            AllFans.Add(fanVm);

            switch (fanVm.DeviceType)
            {
                case CoolingDeviceType.CpuFan: CpuFans.Add(fanVm); break;
                case CoolingDeviceType.WaterPump: WaterPumps.Add(fanVm); break;
                case CoolingDeviceType.GpuFan: GpuFans.Add(fanVm); break;
                default: CaseFans.Add(fanVm); break;
            }
        }

        Task.Run(async () =>
        {
            while (!_isShuttingDown)
            {
                HardwareTemperatureService.Instance.UpdateSensors();

                _dispatcherQueue?.TryEnqueue(() =>
                {
                    if (_isShuttingDown) return;

                    foreach (var fan in AllFans)
                    {
                        fan.UpdateReadings();
                    }
                });

                await Task.Delay(1000);
            }
        });

        _isInitialized = true;
    }

    public void ApplyGlobalPreset(int mode)
    {
        foreach (var fan in AllFans)
        {
            fan.CurvePoints.Clear();

            if (mode == 0) // Silent
            {
                fan.CurvePoints.Add(new FanCurvePoint(40, 20));
                fan.CurvePoints.Add(new FanCurvePoint(60, 40));
                fan.CurvePoints.Add(new FanCurvePoint(75, 60));
                fan.CurvePoints.Add(new FanCurvePoint(85, 100));
            }
            else if (mode == 1) // Balanced
            {
                fan.CurvePoints.Add(new FanCurvePoint(30, 30));
                fan.CurvePoints.Add(new FanCurvePoint(50, 50));
                fan.CurvePoints.Add(new FanCurvePoint(70, 75));
                fan.CurvePoints.Add(new FanCurvePoint(85, 100));
            }
            else if (mode == 2) // Extreme
            {
                fan.CurvePoints.Add(new FanCurvePoint(30, 50));
                fan.CurvePoints.Add(new FanCurvePoint(50, 70));
                fan.CurvePoints.Add(new FanCurvePoint(65, 100));
                fan.CurvePoints.Add(new FanCurvePoint(80, 100));
            }

            fan.IsManualControl = true;
            fan.SavePreferences(fan.Name, fan.DeviceType);
        }
    }

    public void Shutdown()
    {
        _isShuttingDown = true;

        var activeFans = AllFans.Where(f => f.IsManualControl).ToList();

        if (activeFans.Count > 0)
        {
            foreach (var fan in activeFans)
                fan.ApplyEmergencyShock();

            System.Threading.Thread.Sleep(1500);

            foreach (var fan in activeFans)
                fan.ApplyEmergencyRelease();

            System.Threading.Thread.Sleep(500);
        }

        HardwareTemperatureService.Instance.Close();
    }
}