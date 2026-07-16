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

            LocalMachineSettingsEngine.CpuWarningTemp = double.IsNaN(NumCpuWarn.Value) ? 80 : (int)NumCpuWarn.Value;
            LocalMachineSettingsEngine.CpuMaxTemp = double.IsNaN(NumCpuMax.Value) ? 95 : (int)NumCpuMax.Value;

            LocalMachineSettingsEngine.GpuWarningTemp = double.IsNaN(NumGpuWarn.Value) ? 80 : (int)NumGpuWarn.Value;
            LocalMachineSettingsEngine.GpuMaxTemp = double.IsNaN(NumGpuMax.Value) ? 95 : (int)NumGpuMax.Value;

            LocalMachineSettingsEngine.RamWarningTemp = double.IsNaN(NumRamWarn.Value) ? 65 : (int)NumRamWarn.Value;
            LocalMachineSettingsEngine.RamMaxTemp = double.IsNaN(NumRamMax.Value) ? 80 : (int)NumRamMax.Value;

            LocalMachineSettingsEngine.MoboWarningTemp = double.IsNaN(NumMoboWarn.Value) ? 60 : (int)NumMoboWarn.Value;
            LocalMachineSettingsEngine.MoboMaxTemp = double.IsNaN(NumMoboMax.Value) ? 80 : (int)NumMoboMax.Value;
        }
        #endregion
    }
}