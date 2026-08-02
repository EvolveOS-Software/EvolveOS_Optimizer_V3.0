// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class ContextMenuEditorWindow : Window
    {
        #region Win32 Interop

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

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

        #region Fields & Properties

        private ObservableCollection<ModernContextMenuItem> _modernItems = new();
        private ObservableCollection<ClassicContextMenuItem> _classicItems = new();
        private bool _isInitialized = false;

        private List<PresetDisplayItem> _contextMenuPresets = new();
        private bool _isUpdatingPresetToggle = false;

        #endregion

        #region Initialization & Window Setup

        public ContextMenuEditorWindow()
        {
            this.InitializeComponent();

            RootGrid.DataContext = this;

            UIHelper.SetOverlay(true, true);
            ConfigureWindow();

            RootGrid.Loaded += RootElement_Loaded;

            if (Environment.OSVersion.Version.Build < 22000)
            {
                if (ClassicMenuToggleContainer != null)
                {
                    ClassicMenuToggleContainer.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if (ClassicMenuToggle != null)
                {
                    ClassicMenuToggle.IsOn = ContextMenuEngine.IsClassicMenuEnabled();
                    ClassicMenuToggle.Toggled += ClassicMenuToggle_Toggled;
                }
            }

            LoadData();

            _isInitialized = true;
        }

        private void RootElement_Loaded(object sender, RoutedEventArgs e)
        {
            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);
        }

        private void ConfigureWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(wndId);

            var mainWindow = (Application.Current as App)?.GetType().GetProperty("MainWindow")?.GetValue(Application.Current) as Window;
            if (mainWindow != null)
            {
                IntPtr mainHWnd = WindowNative.GetWindowHandle(mainWindow);
                SetWindowLongPtr(hWnd, GWLP_HWNDPARENT, mainHWnd);
            }

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.SetBorderAndTitleBar(true, false);
            }

            double scale = UIHelper.GetScaleAdjustment(hWnd);

            int physicalWidth = (int)(800 * scale);
            int physicalHeight = (int)(750 * scale);

            var displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                int x = displayArea.WorkArea.X + ((displayArea.WorkArea.Width - physicalWidth) / 2);
                int y = displayArea.WorkArea.Y + ((displayArea.WorkArea.Height - physicalHeight) / 2);

                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, physicalWidth, physicalHeight));
            }
        }

        #endregion

        #region Data Loading & Binding

        private async void LoadData()
        {
            // Load Modern JSON
            var config = ContextMenuEngine.LoadModernItems();
            _modernItems = new ObservableCollection<ModernContextMenuItem>(config.Items);

            // Scan the Registry for Classic Items
            var classicItemsList = ContextMenuEngine.GetClassicItems();
            _classicItems = new ObservableCollection<ClassicContextMenuItem>(classicItemsList);

            UpdateListBinding();

            // Load presets asynchronously
            await LoadPresetsAsync();
        }

        private async Task LoadPresetsAsync()
        {
            try
            {
                Debug.WriteLine("[Presets] Starting LoadPresetsAsync...");

                ISettingsLoadingService? settingsService = null;

                try
                {
                    var servicesProp = typeof(App).GetProperty("Services", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (servicesProp != null)
                    {
                        var provider = servicesProp.GetValue(null) as IServiceProvider;
                        settingsService = provider?.GetService(typeof(ISettingsLoadingService)) as ISettingsLoadingService;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Presets] Failed to get via App.Services property: {ex.Message}");
                }

                if (settingsService == null)
                {
                    try
                    {
                        settingsService = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<ISettingsLoadingService>();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Presets] Failed to get via Community Toolkit IOC: {ex.Message}");
                    }
                }

                if (settingsService == null)
                {
                    Debug.WriteLine("[Presets] ERROR: Could not resolve ISettingsLoadingService from any container!");
                    return;
                }

                Debug.WriteLine("[Presets] Found ISettingsLoadingService successfully. Calling LoadConfiguredSettingsAsync...");

                var allExplorerSettings = await settingsService.LoadConfiguredSettingsAsync(
                    FeatureIds.ExplorerCustomization,
                    "Loading presets...",
                    null
                );

                if (allExplorerSettings != null)
                {
                    Debug.WriteLine($"[Presets] Loaded {allExplorerSettings.Count} total Explorer settings.");

                    var rawPresets = allExplorerSettings
                        .Where(s => s.GroupName == "Context Menu" && s.SettingId != "explorer-customization-context-menu")
                        .ToList();

                    _contextMenuPresets = rawPresets
                        .Select(s => new PresetDisplayItem(s))
                        .ToList();

                    Debug.WriteLine($"[Presets] Filtered and wrapped into {_contextMenuPresets.Count} context menu presets.");

                    if (PresetsComboBox != null)
                    {
                        PresetsComboBox.ItemsSource = _contextMenuPresets;

                        if (_contextMenuPresets.Count > 0)
                        {
                            PresetsComboBox.SelectedIndex = 0;
                        }
                        Debug.WriteLine("[Presets] Presets successfully bound to PresetsComboBox!");
                    }
                }
                else
                {
                    Debug.WriteLine("[Presets] LoadConfiguredSettingsAsync returned null!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Presets] EXCEPTION in LoadPresetsAsync: {ex}");
            }
        }

        private void MenuTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateListBinding();

            // Show Presets ONLY if Classic (Index 1) is selected
            if (PresetsContainer != null)
            {
                PresetsContainer.Visibility = MenuTypeSelector.SelectedIndex == 1
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdateListBinding()
        {
            if (ItemsListView == null) return;

            if (MenuTypeSelector.SelectedIndex == 0) // Modern
            {
                ItemsListView.ItemsSource = _modernItems;
            }
            else // Classic
            {
                ItemsListView.ItemsSource = _classicItems;
            }
        }

        #endregion

        #region UI Event Handlers

        private void PresetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetsComboBox.SelectedItem is PresetDisplayItem wrapper && wrapper.Model is SettingItemViewModel selectedPreset)
            {
                _isUpdatingPresetToggle = true;

                if (PresetDescriptionText != null)
                {
                    PresetDescriptionText.Text = selectedPreset.SettingDefinition?.Description ?? string.Empty;
                }

                if (PresetToggleSwitch != null)
                {
                    PresetToggleSwitch.IsEnabled = true;
                    PresetToggleSwitch.IsOn = selectedPreset.IsSelected;
                }

                _isUpdatingPresetToggle = false;
            }
            else
            {
                if (PresetToggleSwitch != null)
                    PresetToggleSwitch.IsEnabled = false;

                if (PresetDescriptionText != null)
                    PresetDescriptionText.Text = string.Empty;
            }
        }

        private async void PresetToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingPresetToggle) return;

            if (PresetsComboBox.SelectedItem is PresetDisplayItem wrapper && wrapper.Model is SettingItemViewModel selectedPreset)
            {
                bool enable = PresetToggleSwitch.IsOn;
                selectedPreset.IsSelected = enable;

                if (selectedPreset.SettingDefinition?.RestartProcess == "Explorer")
                {
                    await ContextMenuEngine.RestartExplorerAsync();
                }
            }
        }

        private async void ClassicMenuToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            bool enableClassic = ClassicMenuToggle.IsOn;

            ContextMenuEngine.ToggleClassicMenu(enableClassic);

            await ContextMenuEngine.RestartExplorerAsync();
        }

        private void BrowseExe_Click(object sender, RoutedEventArgs e)
        {
            IntPtr pFile = Marshal.AllocHGlobal(260 * Marshal.SystemDefaultCharSize);

            try
            {
                Marshal.WriteInt16(pFile, 0);

                var ofn = new OPENFILENAME();
                ofn.lStructSize = Marshal.SizeOf(typeof(OPENFILENAME));
                ofn.hwndOwner = WinRT.Interop.WindowNative.GetWindowHandle(this);

                ofn.lpstrFilter = "Executables (*.exe;*.bat;*.cmd)\0*.exe;*.bat;*.cmd\0All Files (*.*)\0*.*\0";

                ofn.lpstrFile = pFile;
                ofn.nMaxFile = 260;
                ofn.lpstrTitle = "Select Executable";

                ofn.Flags = 0x00080000 | 0x00001000 | 0x00000008;

                if (GetOpenFileName(ref ofn))
                {
                    ExePathInput.Text = Marshal.PtrToStringAuto(ofn.lpstrFile);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pFile);
            }
        }

        private async void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleInput.Text) || string.IsNullOrWhiteSpace(ExePathInput.Text))
                return;

            string targetStr = (TargetInput.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Files";

            if (MenuTypeSelector.SelectedIndex == 0) // Modern
            {
                var newItem = new ModernContextMenuItem
                {
                    Title = TitleInput.Text,
                    ExePath = ExePathInput.Text,
                    Arguments = ArgsInput.Text,
                    Icon = ExePathInput.Text,
                    Target = targetStr
                };

                _modernItems.Add(newItem);
                ContextMenuEngine.SaveModernItems(_modernItems.ToList());

                string packageFolder = Path.Combine(AppContext.BaseDirectory, "ModernMenuPackage");
                await ContextMenuEngine.RegisterSparsePackageAsync(packageFolder);

                await ContextMenuEngine.RestartExplorerAsync();
            }
            else // Classic
            {
                ContextMenuTarget targetEnum = targetStr switch
                {
                    "Folders" => ContextMenuTarget.Folders,
                    "Background" => ContextMenuTarget.Background,
                    _ => ContextMenuTarget.Files
                };

                var newItem = new ClassicContextMenuItem
                {
                    Title = TitleInput.Text,
                    ExecutablePath = ExePathInput.Text,
                    Arguments = ArgsInput.Text,
                    IconPath = ExePathInput.Text,
                    Target = targetEnum
                };

                _classicItems.Add(newItem);
                ContextMenuEngine.AddClassicItem(newItem);
            }

            TitleInput.Text = string.Empty;
            ExePathInput.Text = string.Empty;
            ArgsInput.Text = string.Empty;
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (MenuTypeSelector.SelectedIndex == 0 && btn.Tag is ModernContextMenuItem modernItem)
                {
                    _modernItems.Remove(modernItem);
                    ContextMenuEngine.SaveModernItems(_modernItems.ToList());
                }
                else if (MenuTypeSelector.SelectedIndex == 1 && btn.Tag is ClassicContextMenuItem classicItem)
                {
                    _classicItems.Remove(classicItem);
                    ContextMenuEngine.RemoveClassicItem(classicItem);
                }
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

    #region Helper Class

    public class PresetDisplayItem
    {
        public SettingItemViewModel Model { get; }
        public string Name { get; }

        public PresetDisplayItem(SettingItemViewModel model)
        {
            Model = model;

            string cleaned = model.Name ?? string.Empty;
            cleaned = cleaned.Replace("to Context Menu", "", StringComparison.OrdinalIgnoreCase);
            cleaned = cleaned.Replace("Context Menu", "", StringComparison.OrdinalIgnoreCase);

            Name = cleaned.Trim();
        }
    }

    #endregion
}