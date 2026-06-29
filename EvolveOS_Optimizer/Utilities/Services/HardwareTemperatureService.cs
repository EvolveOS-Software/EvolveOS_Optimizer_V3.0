// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using LibreHardwareMonitor.Hardware;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class HardwareTemperatureService
    {
        public static HardwareTemperatureService Instance { get; } = new HardwareTemperatureService();

        private Computer? _computer;
        private readonly UpdateVisitor _updateVisitor = new UpdateVisitor();

        public void Initialize()
        {
            if (_computer != null) return;

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsMemoryEnabled = true
            };

            _computer.Open();
            _computer.Accept(_updateVisitor);
        }

        public void UpdateSensors()
        {
            _computer?.Accept(_updateVisitor);
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
                    s.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase));

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
            _computer?.Close();
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