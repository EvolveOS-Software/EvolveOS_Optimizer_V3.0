// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class HardwareTemperatureService
    {
        public static HardwareTemperatureService Instance { get; } = new HardwareTemperatureService();

        private Computer? _computer;
        private readonly UpdateVisitor _updateVisitor = new UpdateVisitor();
        private readonly object _hardwareLock = new object();

        private readonly ConcurrentDictionary<ISensor, List<Core.Model.FanCurvePoint>> _activeFanCurves = new();
        private CancellationTokenSource? _fanLoopCts;

        public void Initialize()
        {
            lock (_hardwareLock)
            {
                if (_computer != null) return;

                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMotherboardEnabled = true,
                    IsMemoryEnabled = true,
                    IsControllerEnabled = true,
                    IsStorageEnabled = true
                };

                _computer.Open();
                _computer.Accept(_updateVisitor); // Allowed ONCE at startup to map hardware
            }
        }

        public void UpdateSensors()
        {
            lock (_hardwareLock)
            {
                if (_computer == null) return;

                // ❌ CRITICAL FIX ❌
                // Removed _computer.Accept(_updateVisitor) completely.
                // We ONLY update safe, fast components. Querying Storage (WMI) or Motherboards (SMBus)
                // every 1000ms causes massive DPC latency spikes, freezing the OS and UI.
                foreach (var hw in _computer.Hardware)
                {
                    if (hw.HardwareType == HardwareType.Cpu ||
                        hw.HardwareType == HardwareType.Memory ||
                        hw.HardwareType == HardwareType.GpuNvidia ||
                        hw.HardwareType == HardwareType.GpuAmd ||
                        hw.HardwareType == HardwareType.GpuIntel)
                    {
                        hw.Update();
                    }
                }
            }
        }

        public void UpdateCpuSensors()
        {
            lock (_hardwareLock)
            {
                if (_computer == null) return;

                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        hardware.Update();
                    }
                }
            }
        }

        public float GetCpuTemperature() => GetHardwareTemp(HardwareType.Cpu);
        public float GetMemoryTemperature() => GetHardwareTemp(HardwareType.Memory);
        public float GetMotherboardTemperature() => GetHardwareTemp(HardwareType.Motherboard);

        public float GetGpuTemperature() => Math.Max(
            Math.Max(GetHardwareTemp(HardwareType.GpuNvidia), GetHardwareTemp(HardwareType.GpuAmd)),
            GetHardwareTemp(HardwareType.GpuIntel)
        );

        private float GetHardwareTemp(HardwareType type)
        {
            lock (_hardwareLock)
            {
                if (_computer == null) return 0f;

                var allTempsForThisType = new List<float>();

                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == type)
                    {
                        float temp = GetBestTemperature(hardware);
                        if (temp > 0)
                        {
                            allTempsForThisType.Add(temp);
                        }
                    }
                }

                return allTempsForThisType.Count > 0 ? allTempsForThisType.Max() : 0f;
            }
        }

        private float GetBestTemperature(IHardware hardware)
        {
            var allSensors = new List<ISensor>();
            CollectSensors(hardware, allSensors);

            var tempSensors = allSensors
                .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                .ToList();

            if (tempSensors.Count == 0) return 0f;

            var plausibleSensors = tempSensors
                .Where(s => s.Value.GetValueOrDefault() > 15f && s.Value.GetValueOrDefault() < 115f)
                .ToList();

            if (hardware.HardwareType == HardwareType.Cpu)
            {
                var primary = tempSensors.FirstOrDefault(s =>
                    s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase));

                if (primary != null) return primary.Value.GetValueOrDefault();
                if (plausibleSensors.Count > 0) return plausibleSensors.Max(s => s.Value.GetValueOrDefault());
            }

            if (hardware.HardwareType == HardwareType.GpuNvidia ||
                hardware.HardwareType == HardwareType.GpuAmd ||
                hardware.HardwareType == HardwareType.GpuIntel)
            {
                var primary = tempSensors.FirstOrDefault(s =>
                    s.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains("Edge", StringComparison.OrdinalIgnoreCase));

                if (primary != null) return primary.Value.GetValueOrDefault();
                if (plausibleSensors.Count > 0) return plausibleSensors.Max(s => s.Value.GetValueOrDefault());
            }

            if (hardware.HardwareType == HardwareType.Motherboard)
            {
                var primary = plausibleSensors.FirstOrDefault(s =>
                    s.Name.Contains("System", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains("Chipset", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains("Motherboard", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains("TMPIN", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains("AUXTIN", StringComparison.OrdinalIgnoreCase));

                if (primary != null) return primary.Value.GetValueOrDefault();
                if (plausibleSensors.Count > 0) return plausibleSensors.First().Value.GetValueOrDefault();
            }

            if (hardware.HardwareType == HardwareType.Memory)
            {
                var liveRamSensors = plausibleSensors.Where(s =>
                    !s.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase) &&
                    !s.Name.Contains("Max", StringComparison.OrdinalIgnoreCase) &&
                    !s.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase) &&
                    !s.Name.Contains("Threshold", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                if (liveRamSensors.Count > 0)
                {
                    var primary = liveRamSensors.FirstOrDefault(s =>
                        s.Name.Contains("Temperature", StringComparison.OrdinalIgnoreCase) ||
                        s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase));

                    if (primary != null) return primary.Value.GetValueOrDefault();
                    return liveRamSensors.First().Value.GetValueOrDefault();
                }
            }

            if (plausibleSensors.Count > 0)
            {
                var cleanSensors = plausibleSensors.Where(s =>
                    !s.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase) &&
                    !s.Name.Contains("Max", StringComparison.OrdinalIgnoreCase)).ToList();

                if (cleanSensors.Count > 0) return cleanSensors.First().Value.GetValueOrDefault();
                return plausibleSensors.First().Value.GetValueOrDefault();
            }

            return 0f;
        }

        public Dictionary<string, float> GetCpuCoreTemperatures()
        {
            lock (_hardwareLock)
            {
                var coreTemps = new Dictionary<string, float>();
                if (_computer == null) return coreTemps;

                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        var allSensors = new List<ISensor>();
                        CollectSensors(hardware, allSensors);

                        var coreSensors = allSensors
                            .Where(s => s.SensorType == SensorType.Temperature
                                     && s.Value.HasValue
                                     && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                                     && !s.Name.Contains("Max", StringComparison.OrdinalIgnoreCase)
                                     && !s.Name.Contains("Average", StringComparison.OrdinalIgnoreCase)
                                     && !s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(s => s.Name);

                        foreach (var sensor in coreSensors)
                        {
                            coreTemps[sensor.Name] = sensor.Value.GetValueOrDefault();
                        }
                    }
                }

                return coreTemps;
            }
        }

        public Dictionary<string, float> GetCpuCoreLoads()
        {
            lock (_hardwareLock)
            {
                var coreLoads = new Dictionary<string, float>();
                if (_computer == null) return coreLoads;

                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        var allSensors = new List<ISensor>();
                        CollectSensors(hardware, allSensors);

                        var coreSensors = allSensors
                            .Where(s => s.SensorType == SensorType.Load
                                     && s.Value.HasValue
                                     && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                                     && !s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase)
                                     && !s.Name.Contains("Max", StringComparison.OrdinalIgnoreCase)
                                     && !s.Name.Contains("Average", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(s => s.Name);

                        foreach (var sensor in coreSensors)
                        {
                            coreLoads[sensor.Name] = sensor.Value.GetValueOrDefault();
                        }
                    }
                }

                return coreLoads;
            }
        }

        public float GetGpuPower()
        {
            lock (_hardwareLock)
            {
                try
                {
                    if (_computer == null || _computer.Hardware == null) return 0f;

                    foreach (var hw in _computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd || hw.HardwareType == HardwareType.GpuIntel)
                        {
                            var powerSensors = hw.Sensors.Where(s => s.SensorType == SensorType.Power && s.Value.HasValue).ToList();

                            var packagePower = powerSensors.FirstOrDefault(s =>
                                s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
                                s.Name.Contains("GPU Power", StringComparison.OrdinalIgnoreCase));

                            if (packagePower != null) return packagePower.Value.GetValueOrDefault();

                            return powerSensors.FirstOrDefault()?.Value.GetValueOrDefault() ?? 0f;
                        }
                    }
                    return 0f;
                }
                catch { return 0f; }
            }
        }

        public float GetGpuVramUsedGb()
        {
            lock (_hardwareLock)
            {
                try
                {
                    if (_computer == null || _computer.Hardware == null) return 0f;

                    foreach (var hw in _computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd || hw.HardwareType == HardwareType.GpuIntel)
                        {
                            var dataSensors = hw.Sensors.Where(s => (s.SensorType == SensorType.SmallData || s.SensorType == SensorType.Data) && s.Value.HasValue).ToList();

                            var vramUsed = dataSensors.FirstOrDefault(s =>
                                s.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase) ||
                                s.Name.Contains("GPU Memory Dedicated", StringComparison.OrdinalIgnoreCase));

                            if (vramUsed != null)
                            {
                                return vramUsed.Value.GetValueOrDefault() / 1024f;
                            }
                        }
                    }
                    return 0f;
                }
                catch { return 0f; }
            }
        }

        public float GetCpuPower()
        {
            lock (_hardwareLock)
            {
                try
                {
                    if (_computer == null || _computer.Hardware == null) return 0f;

                    foreach (var hw in _computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.Cpu)
                        {
                            var powerSensors = hw.Sensors.Where(s => s.SensorType == SensorType.Power && s.Value.HasValue).ToList();

                            var packagePower = powerSensors.FirstOrDefault(s =>
                                s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase));

                            if (packagePower != null) return packagePower.Value.GetValueOrDefault();

                            return powerSensors.FirstOrDefault()?.Value.GetValueOrDefault() ?? 0f;
                        }
                    }
                    return 0f;
                }
                catch { return 0f; }
            }
        }

        public float GetCpuClock()
        {
            lock (_hardwareLock)
            {
                try
                {
                    if (_computer == null || _computer.Hardware == null) return 0f;

                    foreach (var hw in _computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.Cpu)
                        {
                            var clockSensors = hw.Sensors.Where(s => s.SensorType == SensorType.Clock && s.Value.HasValue).ToList();

                            var coreClocks = clockSensors.Where(s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)).ToList();

                            if (coreClocks.Count > 0) return coreClocks.Max(s => s.Value.GetValueOrDefault());
                        }
                    }
                    return 0f;
                }
                catch { return 0f; }
            }
        }

        public float GetDiskTemperature()
        {
            lock (_hardwareLock)
            {
                try
                {
                    float highestTemp = -1f;

                    if (_computer == null || _computer.Hardware == null)
                        return highestTemp;

                    foreach (var hardware in _computer.Hardware)
                    {
                        if (hardware.HardwareType == HardwareType.Storage)
                        {
                            // We ONLY update the disk when this method is explicitly called.
                            hardware.Update();

                            var allSensors = new List<ISensor>();
                            CollectSensors(hardware, allSensors);

                            var tempSensors = allSensors
                                .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                                .ToList();

                            var primarySensor = tempSensors.FirstOrDefault(s =>
                                s.Name.Equals("Temperature", StringComparison.OrdinalIgnoreCase) ||
                                s.Name.Equals("Temperature 1", StringComparison.OrdinalIgnoreCase) ||
                                s.Name.Contains("Composite", StringComparison.OrdinalIgnoreCase));

                            var activeSensor = primarySensor ?? tempSensors.FirstOrDefault();

                            if (activeSensor != null && activeSensor.Value.HasValue)
                            {
                                float tempVal = activeSensor.Value.GetValueOrDefault();

                                if (tempVal > 0f && tempVal < 85f)
                                {
                                    if (tempVal > highestTemp)
                                    {
                                        highestTemp = tempVal;
                                    }
                                }
                            }
                        }
                    }
                    return highestTemp;
                }
                catch
                {
                    return -1f;
                }
            }
        }

        public float GetDiskTemperatureByIndex(int index)
        {
            lock (_hardwareLock)
            {
                try
                {
                    if (_computer == null || _computer.Hardware == null) return -1f;

                    var storageDrives = _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage).ToList();

                    if (index >= 0 && index < storageDrives.Count)
                    {
                        var hardware = storageDrives[index];

                        // We ONLY update the disk when this method is explicitly called.
                        hardware.Update();

                        var allSensors = new List<ISensor>();
                        CollectSensors(hardware, allSensors);

                        var tempSensors = allSensors
                            .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                            .ToList();

                        var primarySensor = tempSensors.FirstOrDefault(s =>
                            s.Name.Equals("Temperature", StringComparison.OrdinalIgnoreCase) ||
                            s.Name.Equals("Temperature 1", StringComparison.OrdinalIgnoreCase) ||
                            s.Name.Contains("Composite", StringComparison.OrdinalIgnoreCase));

                        var activeSensor = primarySensor ?? tempSensors.FirstOrDefault();

                        if (activeSensor != null && activeSensor.Value.HasValue)
                        {
                            float tempVal = activeSensor.Value.GetValueOrDefault();
                            if (tempVal > 0f && tempVal < 85f)
                            {
                                return tempVal;
                            }
                        }
                    }
                    return -1f;
                }
                catch
                {
                    return -1f;
                }
            }
        }

        public List<string> GetStorageDriveNames()
        {
            lock (_hardwareLock)
            {
                var driveNames = new List<string>();
                try
                {
                    if (_computer != null && _computer.Hardware != null)
                    {
                        var storageHardware = _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage).ToList();
                        foreach (var hw in storageHardware)
                        {
                            driveNames.Add(hw.Name);
                        }
                    }
                }
                catch { }
                return driveNames;
            }
        }

        public List<ISensor> GetFanControlSensors()
        {
            lock (_hardwareLock)
            {
                var controlSensors = new List<ISensor>();
                if (_computer == null) return controlSensors;

                foreach (var hardware in _computer.Hardware)
                {
                    var allSensors = new List<ISensor>();
                    CollectSensors(hardware, allSensors);

                    var controls = allSensors.Where(s => s.SensorType == SensorType.Control && s.Control != null);
                    controlSensors.AddRange(controls);
                }

                return controlSensors;
            }
        }

        public ISensor? GetMatchingRpmSensor(ISensor controlSensor)
        {
            lock (_hardwareLock)
            {
                var hardware = controlSensor.Hardware;
                return hardware.Sensors.FirstOrDefault(s =>
                    s.SensorType == SensorType.Fan &&
                    s.Index == controlSensor.Index);
            }
        }

        public void SetFanSpeed(ISensor controlSensor, float percentage)
        {
            lock (_hardwareLock)
            {
                controlSensor.Control?.SetSoftware(percentage);
            }
        }

        public void RevertFanToDefault(ISensor controlSensor)
        {
            lock (_hardwareLock)
            {
                controlSensor.Control?.SetDefault();
            }
        }

        public void RegisterFanCurve(ISensor controlSensor, List<Core.Model.FanCurvePoint> curve)
        {
            _activeFanCurves[controlSensor] = curve.OrderBy(p => p.Temperature).ToList();
            StartFanMonitoringLoop();
        }

        public void UnregisterFanCurve(ISensor controlSensor)
        {
            _activeFanCurves.TryRemove(controlSensor, out _);
            RevertFanToDefault(controlSensor);
        }

        private void StartFanMonitoringLoop()
        {
            if (_fanLoopCts != null && !_fanLoopCts.IsCancellationRequested) return;
            _fanLoopCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (!_fanLoopCts.Token.IsCancellationRequested)
                {
                    if (!_activeFanCurves.IsEmpty)
                    {
                        float currentCpuTemp = GetCpuTemperature();

                        foreach (var kvp in _activeFanCurves)
                        {
                            ISensor sensor = kvp.Key;
                            List<Core.Model.FanCurvePoint> curve = kvp.Value;

                            float targetSpeed = CalculateInterpolatedSpeed(currentCpuTemp, curve);
                            SetFanSpeed(sensor, targetSpeed);
                        }
                    }

                    await Task.Delay(2000, _fanLoopCts.Token);
                }
            });
        }

        private float CalculateInterpolatedSpeed(float currentTemp, List<Core.Model.FanCurvePoint> curve)
        {
            if (curve == null || curve.Count == 0) return 100f;

            if (currentTemp <= curve.First().Temperature)
                return (float)curve.First().SpeedPercentage;

            if (currentTemp >= curve.Last().Temperature)
                return (float)curve.Last().SpeedPercentage;

            for (int i = 0; i < curve.Count - 1; i++)
            {
                var p1 = curve[i];
                var p2 = curve[i + 1];

                if (currentTemp >= p1.Temperature && currentTemp <= p2.Temperature)
                {
                    float tempRatio = (float)((currentTemp - p1.Temperature) / (p2.Temperature - p1.Temperature));
                    return (float)(p1.SpeedPercentage + (tempRatio * (p2.SpeedPercentage - p1.SpeedPercentage)));
                }
            }
            return 100f;
        }

        private void CollectSensors(IHardware hardware, List<ISensor> sensors)
        {
            sensors.AddRange(hardware.Sensors);
            foreach (var sub in hardware.SubHardware)
            {
                CollectSensors(sub, sensors);
            }
        }

        public void Close()
        {
            lock (_hardwareLock)
            {
                _fanLoopCts?.Cancel();
                _computer?.Close();
            }
        }
    }

    #region Visitor Class

    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    #endregion
}