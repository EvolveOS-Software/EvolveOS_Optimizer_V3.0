// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Win32;
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

        // ORIGINAL: Advanced Settings Engine Items
        private List<PresetDisplayItem> _contextMenuPresets = new();
        private bool _isUpdatingPresetToggle = false;

        // Quick Templates
        private List<ContextMenuTemplate> _quickTemplates = new();

        // Context Menu Cleaner Items
        private ObservableCollection<CleanerItem> _cleanerItems = new();

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

            if (IsSubMenuToggle != null)
            {
                IsSubMenuToggle.Toggled += IsSubMenuToggle_Toggled;
            }

            LoadData();
            LoadCleanerItems();

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

            int physicalWidth = (int)(900 * scale);
            int physicalHeight = (int)(800 * scale);

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

            // Load Quick Templates
            LoadQuickTemplates();

            // Load Advanced Presets
            await LoadPresetsAsync();
        }

        private void LoadQuickTemplates()
        {
            _quickTemplates = new List<ContextMenuTemplate>
            {
                #region ORIGINAL PRESETS

                new ContextMenuTemplate { Title = "Take Ownership", Description = "Grants full administrator permissions to the selected file", ExePath = "cmd.exe", Arguments = "/c takeown /f \"%1\" /r /d y && icacls \"%1\" /grant administrators:F /t", TargetIndex = 0, RunAsAdmin = true },
                new ContextMenuTemplate { Title = "Open Command Prompt Here", Description = "Opens a standard command prompt in the selected directory", ExePath = "cmd.exe", Arguments = "/s /k pushd \"%V\"", TargetIndex = 1 },
                new ContextMenuTemplate { Title = "Restart Windows Explorer", Description = "Force restarts the explorer.exe process from the desktop", ExePath = "cmd.exe", Arguments = "/c taskkill /f /im explorer.exe & start explorer.exe", TargetIndex = 2, HiddenWindow = true },
                new ContextMenuTemplate { Title = "Copy File Path to Clipboard", Description = "Copies the full path of the selected file", ExePath = "cmd.exe", Arguments = "/c echo \"%1\" | clip", TargetIndex = 0 },
                new ContextMenuTemplate { Title = "Permanently Delete", Description = "Bypasses the Recycle Bin to permanently delete the file", ExePath = "cmd.exe", Arguments = "/c del /f /q \"%1\"", TargetIndex = 0 },
                new ContextMenuTemplate { Title = "Lock PC", Description = "Instantly locks your Windows session", ExePath = "rundll32.exe", Arguments = "user32.dll,LockWorkStation", TargetIndex = 2 },

                #endregion

                #region FILE OPERATIONS (TargetIndex = 0)

                new ContextMenuTemplate { Title = "Open with Notepad", Description = "Forces any unknown file to open in Notepad", ExePath = "notepad.exe", Arguments = "\"%1\"", TargetIndex = 0 },
                new ContextMenuTemplate { Title = "Run PowerShell Script", Description = "Executes the script while bypassing execution policies", ExePath = "powershell.exe", Arguments = "-ExecutionPolicy Bypass -NoExit -File \"%1\"", TargetIndex = 0 },
                new ContextMenuTemplate { Title = "Block Executable in Firewall", Description = "Creates an outbound Windows Firewall rule to block the app", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Start-Process cmd -ArgumentList '/c netsh advfirewall firewall add rule name=\\\"Block %1\\\" dir=out program=\\\"%1\\\" action=block' -Verb RunAs", TargetIndex = 0, RunAsAdmin = true, HiddenWindow = true },
                new ContextMenuTemplate { Title = "Register DLL / OCX", Description = "Registers the library using regsvr32", ExePath = "regsvr32.exe", Arguments = "\"%1\"", TargetIndex = 0, RunAsAdmin = true },
                new ContextMenuTemplate { Title = "Unregister DLL / OCX", Description = "Unregisters the library using regsvr32", ExePath = "regsvr32.exe", Arguments = "/u \"%1\"", TargetIndex = 0, RunAsAdmin = true },
                new ContextMenuTemplate { Title = "Get SHA256 Hash", Description = "Calculates the SHA256 checksum for verification", ExePath = "powershell.exe", Arguments = "-NoExit -Command Get-FileHash -Algorithm SHA256 -Path '%1' | Format-List", TargetIndex = 0 },
                new ContextMenuTemplate { Title = "Get MD5 Hash", Description = "Calculates the MD5 checksum for verification", ExePath = "powershell.exe", Arguments = "-NoExit -Command Get-FileHash -Algorithm MD5 -Path '%1' | Format-List", TargetIndex = 0 },
                new ContextMenuTemplate { Title = "Extract Archive Here (Tar/Zip)", Description = "Extracts the archive contents using built-in Windows Tar", ExePath = "tar.exe", Arguments = "-xf \"%1\"", TargetIndex = 0 },

                #endregion

                #region FOLDER OPERATIONS (TargetIndex = 1)

                new ContextMenuTemplate { Title = "Open PowerShell Here", Description = "Opens a PowerShell window in the selected directory", ExePath = "powershell.exe", Arguments = "-NoExit -Command Set-Location -LiteralPath '%V'", TargetIndex = 1 },
                new ContextMenuTemplate { Title = "Open CMD Here (Admin)", Description = "Opens an elevated command prompt in the selected directory", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Start-Process cmd -ArgumentList '/s /k pushd \\\"%V\\\"' -Verb RunAs", TargetIndex = 1, RunAsAdmin = true },
                new ContextMenuTemplate { Title = "Open PowerShell Here (Admin)", Description = "Opens an elevated PowerShell window in the selected directory", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Start-Process powershell -ArgumentList '-NoExit -Command Set-Location -LiteralPath \\\"%V\\\"' -Verb RunAs", TargetIndex = 1, RunAsAdmin = true },
                new ContextMenuTemplate { Title = "Copy Folder Path to Clipboard", Description = "Copies the full path of the selected folder", ExePath = "cmd.exe", Arguments = "/c echo \"%V\" | clip", TargetIndex = 1 },

                #endregion

                #region SYSTEM / BACKGROUND TOOLS (TargetIndex = 2)

                new ContextMenuTemplate { Title = "Open Task Manager", Description = "Launches the Windows Task Manager", ExePath = "taskmgr.exe", Arguments = "", TargetIndex = 2 },
                new ContextMenuTemplate { Title = "Open Registry Editor", Description = "Launches the Windows Registry Editor", ExePath = "regedit.exe", Arguments = "", TargetIndex = 2 },
                new ContextMenuTemplate { Title = "Open Control Panel", Description = "Launches the legacy Control Panel", ExePath = "control.exe", Arguments = "", TargetIndex = 2 },
                new ContextMenuTemplate { Title = "Open System Properties", Description = "Opens advanced system settings", ExePath = "control.exe", Arguments = "sysdm.cpl", TargetIndex = 2 },
                new ContextMenuTemplate { Title = "Open Services", Description = "Opens the Windows Services management console", ExePath = "mmc.exe", Arguments = "services.msc", TargetIndex = 2 },
                new ContextMenuTemplate { Title = "Access God Mode", Description = "Opens the master Control Panel view", ExePath = "explorer.exe", Arguments = "shell:::{ED7BA470-8E54-465E-825C-99712043E01C}", TargetIndex = 2 },
                new ContextMenuTemplate { Title = "Flush DNS", Description = "Clears the DNS resolver cache", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Start-Process cmd -ArgumentList '/c ipconfig /flushdns & pause' -Verb RunAs", TargetIndex = 2, RunAsAdmin = true },
                new ContextMenuTemplate { Title = "Advanced Startup Options", Description = "Restarts the PC into the Advanced Recovery environment", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Start-Process shutdown -ArgumentList '/r /o /f /t 0' -Verb RunAs", TargetIndex = 2, RunAsAdmin = true, HiddenWindow = true }

                #endregion
            };

            if (QuickTemplatesComboBox != null)
            {
                QuickTemplatesComboBox.ItemsSource = _quickTemplates;
            }
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

                var allExplorerSettings = await settingsService.LoadConfiguredSettingsAsync(
                    FeatureIds.ExplorerCustomization,
                    "Loading presets...",
                    null
                );

                if (allExplorerSettings != null)
                {
                    var rawPresets = allExplorerSettings
                        .Where(s => s.GroupName == "Context Menu" && s.SettingId != "explorer-customization-context-menu")
                        .ToList();

                    _contextMenuPresets = rawPresets
                        .Select(s => new PresetDisplayItem(s))
                        .ToList();

                    if (PresetsComboBox != null)
                    {
                        PresetsComboBox.ItemsSource = _contextMenuPresets;
                        Debug.WriteLine("[Presets] Presets successfully bound to PresetsComboBox!");
                    }
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

            // Show Advanced Presets ONLY if Classic (Index 1) is selected
            if (AdvancedPresetsContainer != null)
            {
                AdvancedPresetsContainer.Visibility = MenuTypeSelector.SelectedIndex == 1
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdateListBinding()
        {
            if (ItemsTreeView == null) return;

            ItemsTreeView.RootNodes.Clear();
            var subMenuFolders = new List<object>();

            if (MenuTypeSelector.SelectedIndex == 0) // Modern
            {
                foreach (var item in _modernItems)
                {
                    ItemsTreeView.RootNodes.Add(CreateModernNode(item, subMenuFolders));
                }
            }
            else // Classic
            {
                foreach (var item in _classicItems)
                {
                    ItemsTreeView.RootNodes.Add(CreateClassicNode(item, subMenuFolders));
                }
            }

            if (ParentMenuComboBox != null)
            {
                ParentMenuComboBox.ItemsSource = subMenuFolders;
                ParentMenuComboBox.SelectedIndex = -1;
            }
        }

        private TreeViewNode CreateModernNode(ModernContextMenuItem item, List<object> subMenuFolders)
        {
            var node = new TreeViewNode { Content = item, IsExpanded = true };
            if (item.IsSubMenu) subMenuFolders.Add(item);

            foreach (var child in item.SubItems)
            {
                node.Children.Add(CreateModernNode(child, subMenuFolders));
            }
            return node;
        }

        private TreeViewNode CreateClassicNode(ClassicContextMenuItem item, List<object> subMenuFolders)
        {
            var node = new TreeViewNode { Content = item, IsExpanded = true };
            if (item.IsSubMenu) subMenuFolders.Add(item);

            foreach (var child in item.SubItems)
            {
                node.Children.Add(CreateClassicNode(child, subMenuFolders));
            }
            return node;
        }

        #endregion

        #region UI Event Handlers (Context Menu Cleaner)

        private void LoadCleanerItems()
        {
            _cleanerItems.Clear();

            string[] basePaths = {
                @"*\shellex\ContextMenuHandlers",
                @"Directory\shellex\ContextMenuHandlers",
                @"Directory\Background\shellex\ContextMenuHandlers"
            };

            foreach (var path in basePaths)
            {
                try
                {
                    using var key = Registry.ClassesRoot.OpenSubKey(path);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        if (subKeyName.Contains("Open With", StringComparison.OrdinalIgnoreCase) ||
                            subKeyName.Contains("Sharing", StringComparison.OrdinalIgnoreCase) ||
                            subKeyName.Contains("WorkFolders", StringComparison.OrdinalIgnoreCase))
                            continue;

                        using var subKey = key.OpenSubKey(subKeyName);
                        bool isEnabled = subKey?.GetValue("LegacyDisable") == null;

                        string displayName = subKeyName;

                        string? clsid = subKeyName.StartsWith("{") ? subKeyName : subKey?.GetValue("") as string;

                        if (!string.IsNullOrEmpty(clsid) && clsid.StartsWith("{") && clsid.EndsWith("}"))
                        {
                            try
                            {
                                using var clsidKey = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}");
                                string? friendlyName = clsidKey?.GetValue("") as string;

                                if (!string.IsNullOrWhiteSpace(friendlyName))
                                {
                                    displayName = friendlyName;
                                }
                            }
                            catch { }
                        }

                        if (!_cleanerItems.Any(c => c.Name == subKeyName && c.TargetPath == path))
                        {
                            _cleanerItems.Add(new CleanerItem
                            {
                                Name = subKeyName,
                                DisplayName = displayName,
                                TargetPath = path,
                                IsEnabled = isEnabled
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Cleaner] Error scanning registry: {ex.Message}");
                }
            }

            if (CleanerListView != null)
            {
                CleanerListView.ItemsSource = _cleanerItems;
            }
        }

        private void CleanerToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (sender is ToggleSwitch toggle && toggle.Tag is CleanerItem item)
            {
                try
                {
                    using var key = Registry.ClassesRoot.OpenSubKey($@"{item.TargetPath}\{item.Name}", true);
                    if (key != null)
                    {
                        if (toggle.IsOn)
                        {
                            key.DeleteValue("LegacyDisable", false);
                        }
                        else
                        {
                            key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("[Cleaner] Requires Admin privileges to modify ContextMenuHandlers.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Cleaner] Failed to toggle item: {ex.Message}");
                }
            }
        }

        #endregion

        #region UI Event Handlers (Import / Export)

        private async void ExportConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var exportData = new ContextMenuExportData { Modern = _modernItems.ToList(), Classic = _classicItems.ToList() };
                string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });

                string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string filePath = Path.Combine(docsPath, "EvolveOS_ContextMenuBackup.json");
                await File.WriteAllTextAsync(filePath, json);

                Debug.WriteLine($"[Export] Exported Context Menu config to {filePath}");

                ContentDialog successDialog = new ContentDialog
                {
                    Title = "Export Successful",
                    Content = $"Your context menu configuration has been successfully backed up to:\n\n{filePath}",
                    CloseButtonText = "OK",
                    XamlRoot = RootGrid.XamlRoot
                };

                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Export] Failed to export config: {ex}");

                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Export Failed",
                    Content = $"An error occurred while exporting your configuration:\n\n{ex.Message}",
                    CloseButtonText = "Close",
                    XamlRoot = RootGrid.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
        }

        private async void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".json");

                Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
                if (file == null) return;

                string json = await File.ReadAllTextAsync(file.Path);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var importedData = JsonSerializer.Deserialize<ContextMenuExportData>(json, options);

                if (importedData != null)
                {
                    // --- Process Modern Items ---
                    if (importedData.Modern != null)
                    {
                        _modernItems.Clear();
                        foreach (var item in importedData.Modern)
                        {
                            _modernItems.Add(item);
                        }

                        ContextMenuEngine.SaveModernItems(_modernItems.ToList());
                        string packageFolder = Path.Combine(AppContext.BaseDirectory, "ModernMenuPackage");
                        await ContextMenuEngine.RegisterSparsePackageAsync(packageFolder);
                    }

                    // --- Process Classic Items ---
                    if (importedData.Classic != null)
                    {
                        foreach (var oldItem in _classicItems)
                        {
                            ContextMenuEngine.RemoveClassicItem(oldItem);
                        }

                        _classicItems.Clear();

                        foreach (var newItem in importedData.Classic)
                        {
                            _classicItems.Add(newItem);
                            ContextMenuEngine.AddClassicItem(newItem);
                        }
                    }

                    UpdateListBinding();

                    await ContextMenuEngine.RestartExplorerAsync();

                    ContentDialog successDialog = new ContentDialog
                    {
                        Title = "Import Successful",
                        Content = "Your context menu configuration has been successfully imported and applied to Windows!",
                        CloseButtonText = "OK",
                        XamlRoot = RootGrid.XamlRoot
                    };

                    await successDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Import] Failed to import config: {ex}");

                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Import Failed",
                    Content = $"An error occurred while importing your configuration file:\n\n{ex.Message}",
                    CloseButtonText = "Close",
                    XamlRoot = RootGrid.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
        }

        #endregion

        #region UI Event Handlers

        private void ViewMode_Checked(object sender, RoutedEventArgs e)
        {
            if (EditorViewContainer == null || CleanerViewContainer == null) return;

            var clickedButton = sender as ToggleButton;

            if (clickedButton == EditorViewButton && EditorViewButton.IsChecked == true)
            {
                CleanerViewButton.IsChecked = false;
                EditorViewContainer.Visibility = Visibility.Visible;
                CleanerViewContainer.Visibility = Visibility.Collapsed;
            }
            else if (clickedButton == CleanerViewButton && CleanerViewButton.IsChecked == true)
            {
                EditorViewButton.IsChecked = false;
                EditorViewContainer.Visibility = Visibility.Collapsed;
                CleanerViewContainer.Visibility = Visibility.Visible;
            }
        }

        private void IsSubMenuToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (CommandInputsContainer != null)
            {
                CommandInputsContainer.Visibility = IsSubMenuToggle.IsOn ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void QuickTemplatesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuickTemplatesComboBox.SelectedItem is ContextMenuTemplate template)
            {
                TitleInput.Text = template.Title;
                ExePathInput.Text = template.ExePath;
                ArgsInput.Text = template.Arguments;
                TargetInput.SelectedIndex = template.TargetIndex;

                if (IconPathInput != null) IconPathInput.Text = template.ExePath;
                if (RunAsAdminToggle != null) RunAsAdminToggle.IsOn = template.RunAsAdmin;
                if (HiddenWindowToggle != null) HiddenWindowToggle.IsOn = template.HiddenWindow;
                if (SpecificExtInput != null) SpecificExtInput.Text = "*";
                if (ExtendedToggle != null) ExtendedToggle.IsOn = false;
                if (PositionInput != null) PositionInput.SelectedIndex = 0;

                if (IsSubMenuToggle != null) IsSubMenuToggle.IsOn = false;

                QuickTemplatesComboBox.SelectedIndex = -1;
            }
        }

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

        private void BrowseIcon_Click(object sender, RoutedEventArgs e)
        {
            IntPtr pFile = Marshal.AllocHGlobal(260 * Marshal.SystemDefaultCharSize);

            try
            {
                Marshal.WriteInt16(pFile, 0);

                var ofn = new OPENFILENAME();
                ofn.lStructSize = Marshal.SizeOf(typeof(OPENFILENAME));
                ofn.hwndOwner = WinRT.Interop.WindowNative.GetWindowHandle(this);

                ofn.lpstrFilter = "Icons (*.ico;*.dll;*.exe)\0*.ico;*.dll;*.exe\0All Files (*.*)\0*.*\0";

                ofn.lpstrFile = pFile;
                ofn.nMaxFile = 260;
                ofn.lpstrTitle = "Select Icon";

                ofn.Flags = 0x00080000 | 0x00001000 | 0x00000008;

                if (GetOpenFileName(ref ofn))
                {
                    if (IconPathInput != null)
                    {
                        IconPathInput.Text = Marshal.PtrToStringAuto(ofn.lpstrFile);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pFile);
            }
        }

        private async void AddItem_Click(object sender, RoutedEventArgs e)
        {
            bool isSubMenu = IsSubMenuToggle != null && IsSubMenuToggle.IsOn;

            if (string.IsNullOrWhiteSpace(TitleInput.Text)) return;
            if (!isSubMenu && string.IsNullOrWhiteSpace(ExePathInput.Text)) return;

            string targetStr = (TargetInput.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Files";

            string finalExe = isSubMenu ? "" : ExePathInput.Text;
            string finalArgs = isSubMenu ? "" : ArgsInput.Text;
            string finalIcon = IconPathInput != null && !string.IsNullOrWhiteSpace(IconPathInput.Text)
                ? IconPathInput.Text
                : ExePathInput.Text;

            bool runAsAdmin = RunAsAdminToggle != null && RunAsAdminToggle.IsOn;
            bool hiddenWindow = HiddenWindowToggle != null && HiddenWindowToggle.IsOn;
            bool isExtended = ExtendedToggle != null && ExtendedToggle.IsOn;
            string specificExt = SpecificExtInput != null ? SpecificExtInput.Text.Trim() : "*";
            if (string.IsNullOrEmpty(specificExt)) specificExt = "*";
            string positionStr = (PositionInput?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Default";

            if (!isSubMenu && (runAsAdmin || hiddenWindow))
            {
                string verb = runAsAdmin ? "-Verb RunAs" : "";
                string windowStyle = hiddenWindow ? "-WindowStyle Hidden" : "";

                finalArgs = $"{windowStyle} -Command Start-Process \"{finalExe}\" -ArgumentList '{finalArgs.Replace("'", "''")}' {verb}";
                finalExe = "powershell.exe";
            }

            if (MenuTypeSelector.SelectedIndex == 0) // Modern
            {
                var newItem = new ModernContextMenuItem
                {
                    Title = TitleInput.Text,
                    ExePath = finalExe,
                    Arguments = finalArgs,
                    Icon = finalIcon,
                    Target = targetStr,

                    Extended = isExtended,
                    SpecificExtension = specificExt,
                    Position = positionStr,
                    IsSubMenu = isSubMenu
                };

                var parentFolder = ParentMenuComboBox?.SelectedItem as ModernContextMenuItem;
                if (parentFolder != null)
                {
                    parentFolder.SubItems.Add(newItem);
                }
                else
                {
                    _modernItems.Add(newItem);
                }

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
                    ExecutablePath = finalExe,
                    Arguments = finalArgs,
                    IconPath = finalIcon,
                    Target = targetEnum,

                    Extended = isExtended,
                    SpecificExtension = specificExt,
                    Position = positionStr,
                    IsSubMenu = isSubMenu
                };

                var parentFolder = ParentMenuComboBox?.SelectedItem as ClassicContextMenuItem;
                if (parentFolder != null)
                {
                    parentFolder.SubItems.Add(newItem);
                }
                else
                {
                    _classicItems.Add(newItem);
                }

                ContextMenuEngine.AddClassicItem(newItem);
            }

            UpdateListBinding();

            TitleInput.Text = string.Empty;
            ExePathInput.Text = string.Empty;
            ArgsInput.Text = string.Empty;

            if (IconPathInput != null) IconPathInput.Text = string.Empty;
            if (RunAsAdminToggle != null) RunAsAdminToggle.IsOn = false;
            if (HiddenWindowToggle != null) HiddenWindowToggle.IsOn = false;
            if (ExtendedToggle != null) ExtendedToggle.IsOn = false;
            if (SpecificExtInput != null) SpecificExtInput.Text = string.Empty;
            if (PositionInput != null) PositionInput.SelectedIndex = 0;
            if (IsSubMenuToggle != null) IsSubMenuToggle.IsOn = false;
            if (ParentMenuComboBox != null) ParentMenuComboBox.SelectedIndex = -1;
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (MenuTypeSelector.SelectedIndex == 0 && btn.Tag is ModernContextMenuItem modernItem)
                {
                    RemoveModernItem(_modernItems, modernItem);
                    ContextMenuEngine.SaveModernItems(_modernItems.ToList());
                }
                else if (MenuTypeSelector.SelectedIndex == 1 && btn.Tag is ClassicContextMenuItem classicItem)
                {
                    RemoveClassicItem(_classicItems, classicItem);
                    ContextMenuEngine.RemoveClassicItem(classicItem);
                }

                UpdateListBinding();
            }
        }

        private bool RemoveModernItem(ICollection<ModernContextMenuItem> list, ModernContextMenuItem target)
        {
            if (list.Remove(target)) return true;
            foreach (var item in list)
            {
                if (RemoveModernItem(item.SubItems, target)) return true;
            }
            return false;
        }

        private bool RemoveClassicItem(ICollection<ClassicContextMenuItem> list, ClassicContextMenuItem target)
        {
            if (list.Remove(target)) return true;
            foreach (var item in list)
            {
                if (RemoveClassicItem(item.SubItems, target)) return true;
            }
            return false;
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

    #region Helper Classes

    public class PresetDisplayItem
    {
        public SettingItemViewModel Model { get; }
        public string Name { get; }

        public PresetDisplayItem(SettingItemViewModel model)
        {
            Model = model;

            string cleaned = model.Name ?? string.Empty;
            cleaned = cleaned.Replace("in Context Menu", "", StringComparison.OrdinalIgnoreCase);
            cleaned = cleaned.Replace("to Context Menu", "", StringComparison.OrdinalIgnoreCase);
            cleaned = cleaned.Replace("Context Menu", "", StringComparison.OrdinalIgnoreCase);

            Name = cleaned.Trim();
        }
    }

    public class ContextMenuTemplate
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public int TargetIndex { get; set; }
        public bool RunAsAdmin { get; set; }
        public bool HiddenWindow { get; set; }

        public string TargetName => TargetIndex switch
        {
            0 => "Files",
            1 => "Folders",
            2 => "Background",
            _ => "Unknown"
        };
    }

    public class CleanerItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class ContextMenuExportData
    {
        public List<ModernContextMenuItem> Modern { get; set; } = new();
        public List<ClassicContextMenuItem> Classic { get; set; } = new();
    }

    #endregion
}