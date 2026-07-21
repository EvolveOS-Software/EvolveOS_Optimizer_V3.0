// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using LibreHardwareMonitor.Hardware;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class FanControlViewModel : ObservableObject
{
    private readonly ISensor _controlSensor;
    private readonly ISensor? _rpmSensor;

    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial float CurrentRpm { get; set; }
    [ObservableProperty] public partial float CurrentPercentage { get; set; }
    [ObservableProperty] public partial bool IsManualControl { get; set; }
    [ObservableProperty] public partial CoolingDeviceType DeviceType { get; set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(SelectedSourcesText))] public partial bool MonitorCpu { get; set; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(SelectedSourcesText))] public partial bool MonitorGpu { get; set; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(SelectedSourcesText))] public partial bool MonitorMotherboard { get; set; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(SelectedSourcesText))] public partial bool MonitorMemory { get; set; }

    [ObservableProperty] public partial double StepUpTime { get; set; }
    [ObservableProperty] public partial double StepDownTime { get; set; }

    [ObservableProperty] public partial float MinStartPwm { get; set; }
    [ObservableProperty] public partial bool IsCalibrating { get; set; }
    [ObservableProperty] public partial string CalibrationStatus { get; set; }

    [ObservableProperty] public partial double CpuDotX { get; set; }
    [ObservableProperty] public partial double CpuDotY { get; set; }
    [ObservableProperty] public partial double CpuDotOpacity { get; set; }
    [ObservableProperty] public partial string? CpuDotTooltip { get; set; }

    [ObservableProperty] public partial double GpuDotX { get; set; }
    [ObservableProperty] public partial double GpuDotY { get; set; }
    [ObservableProperty] public partial double GpuDotOpacity { get; set; }
    [ObservableProperty] public partial string? GpuDotTooltip { get; set; }

    [ObservableProperty] public partial double MbDotX { get; set; }
    [ObservableProperty] public partial double MbDotY { get; set; }
    [ObservableProperty] public partial double MbDotOpacity { get; set; }
    [ObservableProperty] public partial string? MbDotTooltip { get; set; }

    [ObservableProperty] public partial double MemDotX { get; set; }
    [ObservableProperty] public partial double MemDotY { get; set; }
    [ObservableProperty] public partial double MemDotOpacity { get; set; }
    [ObservableProperty] public partial string? MemDotTooltip { get; set; }

    private float _currentSmoothedPercentage = -1f;
    private bool _isLoading = true;

    public ObservableCollection<FanCurvePoint> CurvePoints { get; } = new();

    public string SelectedSourcesText
    {
        get
        {
            var sources = new List<string>();
            if (MonitorCpu) sources.Add(ResourceString.GetString("fan_src_cpu"));
            if (MonitorGpu) sources.Add(ResourceString.GetString("fan_src_gpu"));
            if (MonitorMotherboard) sources.Add(ResourceString.GetString("fan_src_mb"));
            if (MonitorMemory) sources.Add(ResourceString.GetString("fan_src_ram"));

            return sources.Count > 0 ? string.Join(", ", sources) : ResourceString.GetString("fan_src_none");
        }
    }

    public FanControlViewModel(ISensor controlSensor, ISensor? rpmSensor)
    {
        _controlSensor = controlSensor;
        _rpmSensor = rpmSensor;

        Name = $"{controlSensor.Hardware.Name} - {controlSensor.Name}";

        CalibrationStatus = ResourceString.GetString("fan_calib_default");

        DetermineDeviceType();
        InitializeDefaultCurve();
        UpdateReadings();
    }

    private void DetermineDeviceType()
    {
        string lowerName = Name.ToLowerInvariant();
        var hwType = _controlSensor.Hardware.HardwareType;

        MonitorCpu = false; MonitorGpu = false;
        MonitorMotherboard = false; MonitorMemory = false;

        if (hwType == HardwareType.GpuNvidia || hwType == HardwareType.GpuAmd || hwType == HardwareType.GpuIntel || lowerName.Contains("gpu"))
        {
            DeviceType = CoolingDeviceType.GpuFan;
            MonitorGpu = true;
        }
        else if (lowerName.Contains("pump") || lowerName.Contains("aio") || lowerName.Contains("water"))
        {
            DeviceType = CoolingDeviceType.WaterPump;
            MonitorCpu = true;
        }
        else if (lowerName.Contains("cpu"))
        {
            DeviceType = CoolingDeviceType.CpuFan;
            MonitorCpu = true;
        }
        else
        {
            DeviceType = CoolingDeviceType.CaseFan;
            MonitorCpu = true;
            MonitorGpu = true;
        }

        StepUpTime = 2.0;
        StepDownTime = 4.0;
        MinStartPwm = 0f;
    }

    private void InitializeDefaultCurve()
    {
        CurvePoints.Clear();
        CurvePoints.Add(new FanCurvePoint(30, 30));
        CurvePoints.Add(new FanCurvePoint(50, 50));
        CurvePoints.Add(new FanCurvePoint(70, 75));
        CurvePoints.Add(new FanCurvePoint(85, 100));
    }

    public void LoadPreferences()
    {
        try
        {
            _isLoading = true;
            string id = _controlSensor.Identifier.ToString();

            this.Name = LocalMachineSettingsEngine.GetDynamicSetting($"FanName_{id}", this.Name)?.ToString() ?? this.Name;
            this.DeviceType = (CoolingDeviceType)Convert.ToInt32(LocalMachineSettingsEngine.GetDynamicSetting($"FanType_{id}", (int)this.DeviceType));
            this.IsManualControl = Convert.ToBoolean(LocalMachineSettingsEngine.GetDynamicSetting($"FanManual_{id}", false));

            double legacyResponse = Convert.ToDouble(LocalMachineSettingsEngine.GetDynamicSetting($"FanResponse_{id}", 2.0));
            this.StepUpTime = Convert.ToDouble(LocalMachineSettingsEngine.GetDynamicSetting($"FanStepUp_{id}", legacyResponse));
            this.StepDownTime = Convert.ToDouble(LocalMachineSettingsEngine.GetDynamicSetting($"FanStepDown_{id}", legacyResponse * 2));
            this.MinStartPwm = Convert.ToSingle(LocalMachineSettingsEngine.GetDynamicSetting($"FanMinPwm_{id}", 0f));

            this.MonitorCpu = Convert.ToBoolean(LocalMachineSettingsEngine.GetDynamicSetting($"FanSrcCpu_{id}", this.MonitorCpu));
            this.MonitorGpu = Convert.ToBoolean(LocalMachineSettingsEngine.GetDynamicSetting($"FanSrcGpu_{id}", this.MonitorGpu));
            this.MonitorMotherboard = Convert.ToBoolean(LocalMachineSettingsEngine.GetDynamicSetting($"FanSrcMb_{id}", this.MonitorMotherboard));
            this.MonitorMemory = Convert.ToBoolean(LocalMachineSettingsEngine.GetDynamicSetting($"FanSrcMem_{id}", this.MonitorMemory));

            string curveString = LocalMachineSettingsEngine.GetDynamicSetting($"FanCurve_{id}", "")?.ToString() ?? "";

            if (!string.IsNullOrWhiteSpace(curveString))
            {
                var points = curveString.Split('|');
                if (points.Length >= 4)
                {
                    CurvePoints.Clear();
                    foreach (var pt in points)
                    {
                        var parts = pt.Split(':');
                        if (parts.Length == 2 &&
                            double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double t) &&
                            double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double s))
                        {
                            CurvePoints.Add(new FanCurvePoint(t, s));
                        }
                    }

                    if (CurvePoints.Count < 4) InitializeDefaultCurve();
                }
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void SavePreferences(string customName, CoolingDeviceType newType)
    {
        this.Name = customName;
        this.DeviceType = newType;

        string id = _controlSensor.Identifier.ToString();

        LocalMachineSettingsEngine.SetDynamicSetting($"FanName_{id}", customName);
        LocalMachineSettingsEngine.SetDynamicSetting($"FanType_{id}", (int)newType);
        LocalMachineSettingsEngine.SetDynamicSetting($"FanManual_{id}", this.IsManualControl);

        LocalMachineSettingsEngine.SetDynamicSetting($"FanStepUp_{id}", this.StepUpTime);
        LocalMachineSettingsEngine.SetDynamicSetting($"FanStepDown_{id}", this.StepDownTime);
        LocalMachineSettingsEngine.SetDynamicSetting($"FanMinPwm_{id}", this.MinStartPwm);

        LocalMachineSettingsEngine.SetDynamicSetting($"FanSrcCpu_{id}", this.MonitorCpu);
        LocalMachineSettingsEngine.SetDynamicSetting($"FanSrcGpu_{id}", this.MonitorGpu);
        LocalMachineSettingsEngine.SetDynamicSetting($"FanSrcMb_{id}", this.MonitorMotherboard);
        LocalMachineSettingsEngine.SetDynamicSetting($"FanSrcMem_{id}", this.MonitorMemory);

        var curveString = string.Join("|", CurvePoints.OrderBy(p => p.Temperature)
            .Select(p => string.Format(CultureInfo.InvariantCulture, "{0}:{1}", p.Temperature, p.SpeedPercentage)));

        LocalMachineSettingsEngine.SetDynamicSetting($"FanCurve_{id}", curveString);
    }

    partial void OnMonitorCpuChanged(bool value) { if (!_isLoading) SavePreferences(this.Name, this.DeviceType); }
    partial void OnMonitorGpuChanged(bool value) { if (!_isLoading) SavePreferences(this.Name, this.DeviceType); }
    partial void OnMonitorMotherboardChanged(bool value) { if (!_isLoading) SavePreferences(this.Name, this.DeviceType); }
    partial void OnMonitorMemoryChanged(bool value) { if (!_isLoading) SavePreferences(this.Name, this.DeviceType); }
    partial void OnStepUpTimeChanged(double value) { if (!_isLoading) SavePreferences(this.Name, this.DeviceType); }
    partial void OnStepDownTimeChanged(double value) { if (!_isLoading) SavePreferences(this.Name, this.DeviceType); }

    public async Task CalibrateFanAsync()
    {
        if (IsCalibrating) return;

        IsCalibrating = true;
        IsManualControl = true;
        CalibrationStatus = ResourceString.GetString("fan_calib_testing_zero");

        HardwareTemperatureService.Instance.SetFanSpeed(_controlSensor, 0);
        await Task.Delay(4000);

        float testPwm = 10f;
        MinStartPwm = 0f;

        while (testPwm <= 100f)
        {
            CalibrationStatus = string.Format(ResourceString.GetString("fan_calib_testing_val"), testPwm);
            HardwareTemperatureService.Instance.SetFanSpeed(_controlSensor, testPwm);
            await Task.Delay(3000);
            _controlSensor.Hardware.Update();

            if (_rpmSensor != null && _rpmSensor.Value.HasValue && _rpmSensor.Value > 150)
            {
                MinStartPwm = testPwm;
                break;
            }
            testPwm += 5f;
        }

        CalibrationStatus = MinStartPwm > 0 ? string.Format(ResourceString.GetString("fan_calib_starts_at"), MinStartPwm) : ResourceString.GetString("fan_calib_failed");
        SavePreferences(Name, DeviceType);

        await Task.Delay(3000);
        IsCalibrating = false;
        CalibrationStatus = ResourceString.GetString("fan_calib_default");
    }

    public void UpdateReadings()
    {
        if (IsCalibrating) return;

        if (_rpmSensor != null && _rpmSensor.Value.HasValue)
        {
            CurrentRpm = _rpmSensor.Value.Value;
        }

        float maxTemp = 0f;
        float cpuT = HardwareTemperatureService.Instance.GetCpuTemperature();
        float gpuT = HardwareTemperatureService.Instance.GetGpuTemperature();
        float mbT = HardwareTemperatureService.Instance.GetMotherboardTemperature();
        float memT = HardwareTemperatureService.Instance.GetMemoryTemperature();

        if (MonitorCpu)
        {
            maxTemp = Math.Max(maxTemp, cpuT);
            CpuDotX = Math.Clamp(((cpuT - 20) / 80.0) * 400.0, 0, 400);
            CpuDotY = Math.Clamp(150.0 - (CalculateCurveSpeed(cpuT) / 100.0 * 150.0), 0, 150);
            CpuDotTooltip = string.Format(ResourceString.GetString("fan_dot_tooltip_cpu"), cpuT);
            CpuDotOpacity = 1.0;
        }
        else CpuDotOpacity = 0.0;

        if (MonitorGpu)
        {
            maxTemp = Math.Max(maxTemp, gpuT);
            GpuDotX = Math.Clamp(((gpuT - 20) / 80.0) * 400.0, 0, 400);
            GpuDotY = Math.Clamp(150.0 - (CalculateCurveSpeed(gpuT) / 100.0 * 150.0), 0, 150);
            GpuDotTooltip = string.Format(ResourceString.GetString("fan_dot_tooltip_gpu"), gpuT);
            GpuDotOpacity = 1.0;
        }
        else GpuDotOpacity = 0.0;

        if (MonitorMotherboard)
        {
            maxTemp = Math.Max(maxTemp, mbT);
            MbDotX = Math.Clamp(((mbT - 20) / 80.0) * 400.0, 0, 400);
            MbDotY = Math.Clamp(150.0 - (CalculateCurveSpeed(mbT) / 100.0 * 150.0), 0, 150);
            MbDotTooltip = string.Format(ResourceString.GetString("fan_dot_tooltip_mb"), mbT);
            MbDotOpacity = 1.0;
        }
        else MbDotOpacity = 0.0;

        if (MonitorMemory)
        {
            maxTemp = Math.Max(maxTemp, memT);
            MemDotX = Math.Clamp(((memT - 20) / 80.0) * 400.0, 0, 400);
            MemDotY = Math.Clamp(150.0 - (CalculateCurveSpeed(memT) / 100.0 * 150.0), 0, 150);
            MemDotTooltip = string.Format(ResourceString.GetString("fan_dot_tooltip_ram"), memT);
            MemDotOpacity = 1.0;
        }
        else MemDotOpacity = 0.0;

        if (maxTemp <= 0f) maxTemp = HardwareTemperatureService.Instance.GetCpuTemperature();
        if (maxTemp <= 0f) maxTemp = 60f;

        if (IsManualControl)
        {
            float targetSpeed = CalculateCurveSpeed(maxTemp);

            if (targetSpeed > 0 && targetSpeed < MinStartPwm)
            {
                targetSpeed = MinStartPwm;
            }

            if (_currentSmoothedPercentage < 0)
                _currentSmoothedPercentage = targetSpeed;

            if (_currentSmoothedPercentage < targetSpeed)
            {
                float maxStep = StepUpTime <= 0 ? 100f : (float)(100.0 / StepUpTime);
                _currentSmoothedPercentage = Math.Min(targetSpeed, _currentSmoothedPercentage + maxStep);
            }
            else if (_currentSmoothedPercentage > targetSpeed)
            {
                float maxStep = StepDownTime <= 0 ? 100f : (float)(100.0 / StepDownTime);
                _currentSmoothedPercentage = Math.Max(targetSpeed, _currentSmoothedPercentage - maxStep);
            }

            HardwareTemperatureService.Instance.SetFanSpeed(_controlSensor, _currentSmoothedPercentage);
            CurrentPercentage = (float)Math.Round(_currentSmoothedPercentage, 1);
        }
        else
        {
            _currentSmoothedPercentage = -1f;
            if (_controlSensor.Value.HasValue)
                CurrentPercentage = (float)Math.Round(_controlSensor.Value.Value, 1);
        }
    }

    private float CalculateCurveSpeed(float currentTemp)
    {
        var sorted = CurvePoints.OrderBy(p => p.Temperature).ToList();

        if (currentTemp <= sorted.First().Temperature) return (float)sorted.First().SpeedPercentage;
        if (currentTemp >= sorted.Last().Temperature) return (float)sorted.Last().SpeedPercentage;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var p1 = sorted[i];
            var p2 = sorted[i + 1];

            if (currentTemp >= p1.Temperature && currentTemp <= p2.Temperature)
            {
                float tempRange = (float)(p2.Temperature - p1.Temperature);
                float speedRange = (float)(p2.SpeedPercentage - p1.SpeedPercentage);
                float progress = (currentTemp - (float)p1.Temperature) / tempRange;

                return (float)p1.SpeedPercentage + (speedRange * progress);
            }
        }

        return 100f;
    }

    public void ApplyManualSpeed(float percentage)
    {
        IsManualControl = true;
        _currentSmoothedPercentage = -1f;
        HardwareTemperatureService.Instance.SetFanSpeed(_controlSensor, percentage);
    }

    public void RevertToAuto()
    {
        IsManualControl = false;
        _currentSmoothedPercentage = -1f;

        if (_controlSensor.Control != null)
        {
            _controlSensor.Control.SetSoftware(100f);
            _controlSensor.Control.SetDefault();
        }

        _controlSensor.Hardware.Update();
        SavePreferences(this.Name, this.DeviceType);
    }

    public void ApplyEmergencyShock()
    {
        if (_controlSensor.Control != null)
            _controlSensor.Control.SetSoftware(100f);
    }

    public void ApplyEmergencyRelease()
    {
        if (_controlSensor.Control != null)
            _controlSensor.Control.SetDefault();
        _controlSensor.Hardware.Update();
    }
}