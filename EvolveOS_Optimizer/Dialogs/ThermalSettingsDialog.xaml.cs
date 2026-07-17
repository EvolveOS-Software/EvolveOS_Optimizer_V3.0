// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class ThermalSettingsDialog : ContentDialog
    {
        #region Constructor
        public ThermalSettingsDialog()
        {
            this.InitializeComponent();
            LoadCurrentSettings();
        }
        #endregion

        #region Methods
        private void LoadCurrentSettings()
        {
            ToggleWarnings.IsOn = LocalMachineSettingsEngine.EnableThermalWarnings;
            ToggleShutdown.IsOn = LocalMachineSettingsEngine.EnableThermalShutdown;
            ToggleAlarms.IsOn = LocalMachineSettingsEngine.EnableAudibleAlarms;
            ToggleLogging.IsOn = LocalMachineSettingsEngine.EnableThermalLogging;

            NumCooldown.Value = LocalMachineSettingsEngine.WarningCooldownMinutes > 0 ? LocalMachineSettingsEngine.WarningCooldownMinutes : 5;
            ComboEmergencyAction.SelectedIndex = LocalMachineSettingsEngine.EmergencyAction >= 0 ? LocalMachineSettingsEngine.EmergencyAction : 0;

            NumEmergencyDelay.Value = LocalMachineSettingsEngine.EmergencyThresholdSeconds > 0 ? LocalMachineSettingsEngine.EmergencyThresholdSeconds : 5;

            NumCpuWarn.Value = LocalMachineSettingsEngine.CpuWarningTemp;
            NumCpuMax.Value = LocalMachineSettingsEngine.CpuMaxTemp;

            NumGpuWarn.Value = LocalMachineSettingsEngine.GpuWarningTemp;
            NumGpuMax.Value = LocalMachineSettingsEngine.GpuMaxTemp;

            NumRamWarn.Value = LocalMachineSettingsEngine.RamWarningTemp;
            NumRamMax.Value = LocalMachineSettingsEngine.RamMaxTemp;

            NumMoboWarn.Value = LocalMachineSettingsEngine.MoboWarningTemp;
            NumMoboMax.Value = LocalMachineSettingsEngine.MoboMaxTemp;
        }

        private void ThermalSettingsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            LocalMachineSettingsEngine.EnableThermalWarnings = ToggleWarnings.IsOn;
            LocalMachineSettingsEngine.EnableThermalShutdown = ToggleShutdown.IsOn;
            LocalMachineSettingsEngine.EnableAudibleAlarms = ToggleAlarms.IsOn;
            LocalMachineSettingsEngine.EnableThermalLogging = ToggleLogging.IsOn;

            LocalMachineSettingsEngine.WarningCooldownMinutes = double.IsNaN(NumCooldown.Value) ? 5 : (int)NumCooldown.Value;
            LocalMachineSettingsEngine.EmergencyAction = ComboEmergencyAction.SelectedIndex;

            int delayVal = double.IsNaN(NumEmergencyDelay.Value) ? 5 : (int)NumEmergencyDelay.Value;
            LocalMachineSettingsEngine.EmergencyThresholdSeconds = Math.Clamp(delayVal, 1, 120);

            int cpuWarn = double.IsNaN(NumCpuWarn.Value) ? 80 : (int)NumCpuWarn.Value;
            int cpuMax = double.IsNaN(NumCpuMax.Value) ? 95 : (int)NumCpuMax.Value;

            int gpuWarn = double.IsNaN(NumGpuWarn.Value) ? 80 : (int)NumGpuWarn.Value;
            int gpuMax = double.IsNaN(NumGpuMax.Value) ? 95 : (int)NumGpuMax.Value;

            int ramWarn = double.IsNaN(NumRamWarn.Value) ? 65 : (int)NumRamWarn.Value;
            int ramMax = double.IsNaN(NumRamMax.Value) ? 80 : (int)NumRamMax.Value;

            int moboWarn = double.IsNaN(NumMoboWarn.Value) ? 60 : (int)NumMoboWarn.Value;
            int moboMax = double.IsNaN(NumMoboMax.Value) ? 80 : (int)NumMoboMax.Value;

            LocalMachineSettingsEngine.CpuWarningTemp = Math.Min(cpuWarn, cpuMax - 5);
            LocalMachineSettingsEngine.CpuMaxTemp = cpuMax;

            LocalMachineSettingsEngine.GpuWarningTemp = Math.Min(gpuWarn, gpuMax - 5);
            LocalMachineSettingsEngine.GpuMaxTemp = gpuMax;

            LocalMachineSettingsEngine.RamWarningTemp = Math.Min(ramWarn, ramMax - 5);
            LocalMachineSettingsEngine.RamMaxTemp = ramMax;

            LocalMachineSettingsEngine.MoboWarningTemp = Math.Min(moboWarn, moboMax - 5);
            LocalMachineSettingsEngine.MoboMaxTemp = moboMax;
        }

        private void ThermalSettingsDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ToggleWarnings.IsOn = true;
            ToggleShutdown.IsOn = false;
            ToggleAlarms.IsOn = false;
            ToggleLogging.IsOn = false;

            NumCooldown.Value = 5;
            ComboEmergencyAction.SelectedIndex = 0;

            NumEmergencyDelay.Value = 5;

            NumCpuWarn.Value = 80; NumCpuMax.Value = 95;
            NumGpuWarn.Value = 80; NumGpuMax.Value = 95;
            NumRamWarn.Value = 65; NumRamMax.Value = 80;
            NumMoboWarn.Value = 60; NumMoboMax.Value = 80;

            args.Cancel = true;
        }
        #endregion
    }
}