// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Runtime.InteropServices;
using EvolveOS_Optimizer.Core.Localization;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Win32;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class PointInTimeRestoreWindow : Window
    {
        #region Win32 Interop for Window Centering
        private const int GWLP_HWNDPARENT = -8;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return SetWindowLong32(hWnd, nIndex, dwNewLong);
        }
        #endregion

        #region Fields
        private bool _isInitialized = false;
        private List<SettingDefinition> _definitions = new();
        #endregion

        #region Constructor
        public PointInTimeRestoreWindow()
        {
            this.InitializeComponent();

            UIHelper.SetOverlay(true, true);
            ConfigureWindow();

            RootGrid.Loaded += RootElement_Loaded;

            LoadDefinitions();
            ApplyDefinitionsToUI();

            ReadCurrentRegistryStates();

            _isInitialized = true;
        }

        private void RootElement_Loaded(object sender, RoutedEventArgs e)
        {
            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);
        }
        #endregion

        #region Window Placement
        private void ConfigureWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(wndId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.SetBorderAndTitleBar(true, false);
            }

            double scale = UIHelper.GetScaleAdjustment(hWnd);
            int physicalWidth = (int)(750 * scale);
            int physicalHeight = (int)(750 * scale);

            var mainWindow = (Application.Current as App)?.GetType().GetProperty("MainWindow")?.GetValue(Application.Current) as Window;

            if (mainWindow != null)
            {
                IntPtr mainHWnd = WindowNative.GetWindowHandle(mainWindow);
                SetWindowLongPtr(hWnd, GWLP_HWNDPARENT, mainHWnd);

                WindowId mainWndId = Win32Interop.GetWindowIdFromWindow(mainHWnd);
                AppWindow mainAppWindow = AppWindow.GetFromWindowId(mainWndId);

                int x = mainAppWindow.Position.X + ((mainAppWindow.Size.Width - physicalWidth) / 2);
                int y = mainAppWindow.Position.Y + ((mainAppWindow.Size.Height - physicalHeight) / 2);

                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, physicalWidth, physicalHeight));
            }
        }
        #endregion

        #region Definition Loading & Parsing
        private void LoadDefinitions()
        {
            var updateGroup = UpdateOptimizations.GetUpdateOptimizations();

            _definitions = updateGroup.Settings
                .Where(s => s.Id != null && s.Id.StartsWith("PointInTimeRestore_"))
                .ToList();
        }

        private void ApplyDefinitionsToUI()
        {
            var stateDef = _definitions.FirstOrDefault(d => d.Id == "PointInTimeRestore_State");
            if (stateDef != null)
            {
                TxtStateTitle.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Name(stateDef));
                TxtStateDesc.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Description(stateDef));
            }

            var freqDef = _definitions.FirstOrDefault(d => d.Id == "PointInTimeRestore_Frequency");
            if (freqDef != null)
            {
                TxtFrequencyTitle.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Name(freqDef));
                TxtFrequencyDesc.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Description(freqDef));
                PopulateComboBox(CbFrequency, freqDef);
            }

            var retDef = _definitions.FirstOrDefault(d => d.Id == "PointInTimeRestore_Retention");
            if (retDef != null)
            {
                TxtRetentionTitle.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Name(retDef));
                TxtRetentionDesc.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Description(retDef));
                PopulateComboBox(CbRetention, retDef);
            }

            var storeDef = _definitions.FirstOrDefault(d => d.Id == "PointInTimeRestore_MaxStorage");
            if (storeDef != null)
            {
                TxtStorageTitle.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Name(storeDef));
                TxtStorageDesc.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Description(storeDef));
                SliderStorage.Minimum = storeDef.NumericRange!.MinValue;
                SliderStorage.Maximum = storeDef.NumericRange.MaxValue;
                SliderStorage.StepFrequency = storeDef.NumericRange.Increment;
            }

            var lockDef = _definitions.FirstOrDefault(d => d.Id == "PointInTimeRestore_HardLock");
            if (lockDef != null)
            {
                TxtHardLockTitle.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Name(lockDef));
                TxtHardLockDesc.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Description(lockDef));
            }

            var snapDef = _definitions.FirstOrDefault(d => d.Id == "PointInTimeRestore_Snapshots");
            if (snapDef != null)
            {
                TxtSnapshotsTitle.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Name(snapDef));
                TxtSnapshotsDesc.Text = LocalizationService.Instance.GetString(SettingLocalizationKeys.Description(snapDef));
            }
        }

        private void PopulateComboBox(ComboBox box, SettingDefinition setting)
        {
            if (setting.ComboBox == null || setting.ComboBox.Options == null) return;

            for (int i = 0; i < setting.ComboBox.Options.Count; i++)
            {
                var opt = setting.ComboBox.Options[i];

                string key = SettingLocalizationKeys.IsLocalizationKey(opt.DisplayName)
                    ? opt.DisplayName
                    : SettingLocalizationKeys.OptionDisplay(setting, i);

                string localizedDisplayName = LocalizationService.Instance.GetString(key);
                if (localizedDisplayName == key)
                {
                    localizedDisplayName = opt.DisplayName;
                }

                box.Items.Add(new ComboBoxItem
                {
                    Content = localizedDisplayName,
                    Tag = opt
                });
            }
        }
        #endregion

        #region Registry Reading & Writing
        private void ReadCurrentRegistryStates()
        {
            int stateVal = ReadRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\Recovery\PITR\Settings", "Active_UX", 0);
            MasterStateToggle.IsOn = (stateVal == 1);
            AdvancedSettingsContainer.Opacity = MasterStateToggle.IsOn ? 1.0 : 0.5;
            AdvancedSettingsContainer.IsHitTestVisible = MasterStateToggle.IsOn;

            int freqVal = ReadRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\Recovery\PITR\Settings", "SnapshotInterval_UX", 1440);
            SelectComboBoxByMappingValue(CbFrequency, "SnapshotInterval_UX", freqVal);

            int retVal = ReadRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\Recovery\PITR\Settings", "MaxTimespan_UX", 4320);
            SelectComboBoxByMappingValue(CbRetention, "MaxTimespan_UX", retVal);

            int storeVal = ReadRegDword(@"SOFTWARE\EvolveOS_Optimizer\Settings", "PITR_MaxStorage", 10);
            SliderStorage.Value = storeVal;

            string formatString = LocalizationService.Instance.GetString("pitr_storage_gb");
            if (formatString == "pitr_storage_gb") formatString = "{0} GB";
            TxtStorageValueDisplay.Text = string.Format(formatString, storeVal);

            int lockVal = ReadRegDword(@"SOFTWARE\EvolveOS_Optimizer\Settings", "PITR_HardLock", 1);
            HardLockToggle.IsOn = (lockVal == 1);
        }

        private int ReadRegDword(string subKey, string valueName, int defaultValue)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(subKey);
                if (key != null)
                {
                    var val = key.GetValue(valueName);
                    if (val is int intVal) return intVal;
                }
            }
            catch { }
            return defaultValue;
        }

        private void WriteRegDword(string subKey, string valueName, int value)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(subKey);
                key?.SetValue(valueName, value, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PITR Window] Failed to write {valueName}: {ex.Message}");
            }
        }

        private void SelectComboBoxByMappingValue(ComboBox box, string mappingKey, int expectedValue)
        {
            foreach (ComboBoxItem item in box.Items)
            {
                if (item.Tag is ComboBoxOption opt && opt.ValueMappings != null && opt.ValueMappings.TryGetValue(mappingKey, out var mapVal))
                {
                    if (Convert.ToInt32(mapVal) == expectedValue)
                    {
                        box.SelectedItem = item;
                        return;
                    }
                }
            }

            var defaultItem = box.Items.Cast<ComboBoxItem>()
                                       .FirstOrDefault(i => i.Tag is ComboBoxOption o && o.IsDefault);
            if (defaultItem != null) box.SelectedItem = defaultItem;
        }
        #endregion

        #region UI Event Handlers
        private void MasterStateToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            int val = MasterStateToggle.IsOn ? 1 : 0;
            WriteRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\Recovery\PITR\Settings", "Active_UX", val);

            AdvancedSettingsContainer.Opacity = MasterStateToggle.IsOn ? 1.0 : 0.5;
            AdvancedSettingsContainer.IsHitTestVisible = MasterStateToggle.IsOn;
        }

        private void CbFrequency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || CbFrequency.SelectedItem is not ComboBoxItem item) return;

            if (item.Tag is ComboBoxOption opt && opt.ValueMappings != null && opt.ValueMappings.TryGetValue("SnapshotInterval_UX", out var val))
            {
                WriteRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\Recovery\PITR\Settings", "SnapshotInterval_UX", Convert.ToInt32(val));
            }
        }

        private void CbRetention_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || CbRetention.SelectedItem is not ComboBoxItem item) return;

            if (item.Tag is ComboBoxOption opt && opt.ValueMappings != null && opt.ValueMappings.TryGetValue("MaxTimespan_UX", out var val))
            {
                WriteRegDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\Recovery\PITR\Settings", "MaxTimespan_UX", Convert.ToInt32(val));
            }
        }

        private async void SliderStorage_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (TxtStorageValueDisplay != null)
            {
                string formatString = LocalizationService.Instance.GetString("pitr_storage_gb");
                if (formatString == "pitr_storage_gb") formatString = "{0} GB";
                TxtStorageValueDisplay.Text = string.Format(formatString, e.NewValue);
            }

            if (!_isInitialized) return;

            WriteRegDword(@"SOFTWARE\EvolveOS_Optimizer\Settings", "PITR_MaxStorage", (int)e.NewValue);

            string sysDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";

            await CommandExecutor.RunCommand($"vssadmin resize shadowstorage /For={sysDrive} /On={sysDrive} /MaxSize={(int)e.NewValue}GB");
        }

        private void HardLockToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            int val = HardLockToggle.IsOn ? 1 : 0;
            WriteRegDword(@"SOFTWARE\EvolveOS_Optimizer\Settings", "PITR_HardLock", val);
        }

        private void BtnViewSnapshots_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "SystemPropertiesProtection.exe",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open System Properties: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            UIHelper.SetOverlay(false);
        }
        #endregion
    }
}