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
using Microsoft.UI.Xaml.Input;
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

            if (ModernMenuToggle != null)
            {
                ModernMenuToggle.IsOn = LocalMachineSettingsEngine.IsModernContextMenuEnabled;
                ModernMenuToggle.Toggled += ModernMenuToggle_Toggled;
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
            else
            {
                var displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    int x = displayArea.WorkArea.X + ((displayArea.WorkArea.Width - physicalWidth) / 2);
                    int y = displayArea.WorkArea.Y + ((displayArea.WorkArea.Height - physicalHeight) / 2);

                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, physicalWidth, physicalHeight));
                }
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
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_takeown_title") ?? "Take Ownership", Description = ResourceString.GetString("cme_qt_takeown_desc") ?? "Grants full administrator permissions to the selected file", ExePath = "cmd.exe", Arguments = "/c takeown /f \"%1\" /r /d y && icacls \"%1\" /grant administrators:F /t", TargetIndex = 0, RunAsAdmin = true },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_cmd_title") ?? "Open Command Prompt Here", Description = ResourceString.GetString("cme_qt_cmd_desc") ?? "Opens a standard command prompt in the selected directory", ExePath = "cmd.exe", Arguments = "/s /k pushd \"%V\"", TargetIndex = 1 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_restart_exp_title") ?? "Restart Windows Explorer", Description = ResourceString.GetString("cme_qt_restart_exp_desc") ?? "Force restarts the explorer.exe process from the desktop", ExePath = "cmd.exe", Arguments = "/c taskkill /f /im explorer.exe & start explorer.exe", TargetIndex = 2, HiddenWindow = true },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_copy_path_title") ?? "Copy File Path to Clipboard", Description = ResourceString.GetString("cme_qt_copy_path_desc") ?? "Copies the full path of the selected file", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Set-Clipboard -Value \"%1\"", TargetIndex = 0 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_perm_del_title") ?? "Permanently Delete", Description = ResourceString.GetString("cme_qt_perm_del_desc") ?? "Bypasses the Recycle Bin to permanently delete the file", ExePath = "cmd.exe", Arguments = "/c del /f /q \"%1\"", TargetIndex = 0 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_lock_pc_title") ?? "Lock PC", Description = ResourceString.GetString("cme_qt_lock_pc_desc") ?? "Instantly locks your Windows session", ExePath = "rundll32.exe", Arguments = "user32.dll,LockWorkStation", TargetIndex = 2 },
                #endregion

                #region FILE OPERATIONS (TargetIndex = 0)
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_notepad_title") ?? "Open with Notepad", Description = ResourceString.GetString("cme_qt_notepad_desc") ?? "Forces any unknown file to open in Notepad", ExePath = "notepad.exe", Arguments = "\"%1\"", TargetIndex = 0 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_ps_script_title") ?? "Run PowerShell Script", Description = ResourceString.GetString("cme_qt_ps_script_desc") ?? "Executes the script while bypassing execution policies", ExePath = "powershell.exe", Arguments = "-ExecutionPolicy Bypass -NoExit -File \"%1\"", TargetIndex = 0 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_block_fw_title") ?? "Block Executable in Firewall", Description = ResourceString.GetString("cme_qt_block_fw_desc") ?? "Creates an outbound Windows Firewall rule to block the app", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Start-Process cmd -ArgumentList '/c netsh advfirewall firewall add rule name=\\\"Block %1\\\" dir=out program=\\\"%1\\\" action=block' -Verb RunAs", TargetIndex = 0, RunAsAdmin = true, HiddenWindow = true },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_reg_dll_title") ?? "Register DLL / OCX", Description = ResourceString.GetString("cme_qt_reg_dll_desc") ?? "Registers the library using regsvr32", ExePath = "regsvr32.exe", Arguments = "\"%1\"", TargetIndex = 0, RunAsAdmin = true },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_unreg_dll_title") ?? "Unregister DLL / OCX", Description = ResourceString.GetString("cme_qt_unreg_dll_desc") ?? "Unregisters the library using regsvr32", ExePath = "regsvr32.exe", Arguments = "/u \"%1\"", TargetIndex = 0, RunAsAdmin = true },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_sha256_title") ?? "Get SHA256 Hash", Description = ResourceString.GetString("cme_qt_sha256_desc") ?? "Calculates the SHA256 checksum for verification", ExePath = "powershell.exe", Arguments = "-NoExit -Command Get-FileHash -Algorithm SHA256 -Path '%1' | Format-List", TargetIndex = 0 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_md5_title") ?? "Get MD5 Hash", Description = ResourceString.GetString("cme_qt_md5_desc") ?? "Calculates the MD5 checksum for verification", ExePath = "powershell.exe", Arguments = "-NoExit -Command Get-FileHash -Algorithm MD5 -Path '%1' | Format-List", TargetIndex = 0 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_extract_tar_title") ?? "Extract Archive Here (Tar/Zip)", Description = ResourceString.GetString("cme_qt_extract_tar_desc") ?? "Extracts the archive contents using built-in Windows Tar", ExePath = "tar.exe", Arguments = "-xf \"%1\"", TargetIndex = 0 },
                #endregion

                #region FOLDER OPERATIONS (TargetIndex = 1)
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_ps_here_title") ?? "Open PowerShell Here", Description = ResourceString.GetString("cme_qt_ps_here_desc") ?? "Opens a PowerShell window in the selected directory", ExePath = "powershell.exe", Arguments = "-NoExit -Command Set-Location -LiteralPath '%V'", TargetIndex = 1 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_cmd_admin_title") ?? "Open CMD Here (Admin)", Description = ResourceString.GetString("cme_qt_cmd_admin_desc") ?? "Opens an elevated command prompt in the selected directory", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Start-Process cmd -ArgumentList '/s /k pushd \\\"%V\\\"' -Verb RunAs", TargetIndex = 1, RunAsAdmin = true },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_ps_admin_title") ?? "Open PowerShell Here (Admin)", Description = ResourceString.GetString("cme_qt_ps_admin_desc") ?? "Opens an elevated PowerShell window in the selected directory", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Start-Process powershell -ArgumentList '-NoExit -Command Set-Location -LiteralPath \\\"%V\\\"' -Verb RunAs", TargetIndex = 1, RunAsAdmin = true },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_copy_folder_title") ?? "Copy Folder Path to Clipboard", Description = ResourceString.GetString("cme_qt_copy_folder_desc") ?? "Copies the full path of the selected folder", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Set-Clipboard -Value \"%V\"", TargetIndex = 1 },
                #endregion

                #region SYSTEM / BACKGROUND TOOLS (TargetIndex = 2)
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_taskmgr_title") ?? "Open Task Manager", Description = ResourceString.GetString("cme_qt_taskmgr_desc") ?? "Launches the Windows Task Manager", ExePath = "taskmgr.exe", Arguments = "", TargetIndex = 2 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_regedit_title") ?? "Open Registry Editor", Description = ResourceString.GetString("cme_qt_regedit_desc") ?? "Launches the Windows Registry Editor", ExePath = "regedit.exe", Arguments = "", TargetIndex = 2 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_control_title") ?? "Open Control Panel", Description = ResourceString.GetString("cme_qt_control_desc") ?? "Launches the legacy Control Panel", ExePath = "control.exe", Arguments = "", TargetIndex = 2 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_sysprop_title") ?? "Open System Properties", Description = ResourceString.GetString("cme_qt_sysprop_desc") ?? "Opens advanced system settings", ExePath = "control.exe", Arguments = "sysdm.cpl", TargetIndex = 2 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_services_title") ?? "Open Services", Description = ResourceString.GetString("cme_qt_services_desc") ?? "Opens the Windows Services management console", ExePath = "mmc.exe", Arguments = "services.msc", TargetIndex = 2 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_godmode_title") ?? "Access God Mode", Description = ResourceString.GetString("cme_qt_godmode_desc") ?? "Opens the master Control Panel view", ExePath = "explorer.exe", Arguments = "shell:::{ED7BA470-8E54-465E-825C-99712043E01C}", TargetIndex = 2 },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_flushdns_title") ?? "Flush DNS", Description = ResourceString.GetString("cme_qt_flushdns_desc") ?? "Clears the DNS resolver cache", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Start-Process cmd -ArgumentList '/c ipconfig /flushdns & pause' -Verb RunAs", TargetIndex = 2, RunAsAdmin = true },
                new ContextMenuTemplate { Title = ResourceString.GetString("cme_qt_adv_startup_title") ?? "Advanced Startup Options", Description = ResourceString.GetString("cme_qt_adv_startup_desc") ?? "Restarts the PC into the Advanced Recovery environment", ExePath = "powershell.exe", Arguments = "-WindowStyle Hidden -Command Start-Process shutdown -ArgumentList '/r /o /f /t 0' -Verb RunAs", TargetIndex = 2, RunAsAdmin = true, HiddenWindow = true }
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
                    ResourceString.GetString("cme_loading_presets") ?? "Loading presets...",
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

            if (AdvancedPresetsContainer != null)
            {
                AdvancedPresetsContainer.Visibility = MenuTypeSelector.SelectedIndex == 1
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdateListBinding()
        {
            if (ItemsListView == null) return;

            if (MenuTypeSelector.SelectedIndex == 0) // Modern
                ItemsListView.ItemsSource = _modernItems;
            else // Classic
                ItemsListView.ItemsSource = _classicItems;
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

            string[] systemBlocklist = {
                "Taskband Pin", "Start Menu Pin", "Offline Files", "Previous Versions",
                "New Menu Handler", "Encryption Context Menu", "EPP", "FileSyncEx",
                "BriefcaseMenu", "Sharing", "Open With", "WorkFolders", "Send To",
                "Library Location", "PinToNameSpaceTree", "PlayTo", "Network",
                "ModernSharing", "Extract"
            };

            foreach (var path in basePaths)
            {
                try
                {
                    using var key = Registry.ClassesRoot.OpenSubKey(path);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
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

                        bool isBlocked = systemBlocklist.Any(b =>
                            subKeyName.Contains(b, StringComparison.OrdinalIgnoreCase) ||
                            displayName.Contains(b, StringComparison.OrdinalIgnoreCase));

                        if (isBlocked) continue;

                        var existingItem = _cleanerItems.FirstOrDefault(c => c.Name == subKeyName || c.DisplayName == displayName);
                        if (existingItem != null)
                        {
                            if (!existingItem.TargetPaths.Contains(path))
                            {
                                existingItem.TargetPaths.Add(path);
                            }
                        }
                        else
                        {
                            _cleanerItems.Add(new CleanerItem
                            {
                                Name = subKeyName,
                                DisplayName = displayName,
                                TargetPaths = new List<string> { path },
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
                foreach (var targetPath in item.TargetPaths)
                {
                    try
                    {
                        using var key = Registry.ClassesRoot.OpenSubKey($@"{targetPath}\{item.Name}", true);
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
                        Debug.WriteLine($"[Cleaner] Requires Admin privileges to modify {item.Name} at {targetPath}.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Cleaner] Failed to toggle item {item.Name}: {ex.Message}");
                    }
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

                ContentDialog successDialog = new ContentDialog
                {
                    Title = ResourceString.GetString("cme_export_success_title") ?? "Export Successful",
                    Content = $"{ResourceString.GetString("cme_export_success_desc") ?? "Your context menu configuration has been successfully backed up to:"}\n\n{filePath}",
                    CloseButtonText = ResourceString.GetString("cme_ok") ?? "OK",
                    XamlRoot = RootGrid.XamlRoot
                };

                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = ResourceString.GetString("cme_export_failed_title") ?? "Export Failed",
                    Content = $"{ResourceString.GetString("cme_export_failed_desc") ?? "An error occurred while exporting your configuration:"}\n\n{ex.Message}",
                    CloseButtonText = ResourceString.GetString("cme_close") ?? "Close",
                    XamlRoot = RootGrid.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
        }

        private async void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            string selectedFile = string.Empty;
            IntPtr pFile = Marshal.AllocHGlobal(260 * Marshal.SystemDefaultCharSize);

            try
            {
                Marshal.WriteInt16(pFile, 0);

                var ofn = new OPENFILENAME();
                ofn.lStructSize = Marshal.SizeOf(typeof(OPENFILENAME));
                ofn.hwndOwner = WindowNative.GetWindowHandle(this);
                ofn.lpstrFilter = "JSON Files (*.json)\0*.json\0All Files (*.*)\0*.*\0";
                ofn.lpstrFile = pFile;
                ofn.nMaxFile = 260;
                ofn.lpstrTitle = ResourceString.GetString("cme_import_config_title") ?? "Import Configuration";
                ofn.Flags = 0x00080000 | 0x00001000 | 0x00000008;

                if (GetOpenFileName(ref ofn))
                {
                    selectedFile = Marshal.PtrToStringAuto(ofn.lpstrFile) ?? string.Empty;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pFile);
            }

            if (string.IsNullOrEmpty(selectedFile)) return;

            try
            {
                string json = await File.ReadAllTextAsync(selectedFile);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var importedData = JsonSerializer.Deserialize<ContextMenuExportData>(json, options);

                if (importedData != null)
                {
                    if (importedData.Modern != null)
                    {
                        _modernItems.Clear();
                        foreach (var item in importedData.Modern)
                        {
                            _modernItems.Add(item);
                        }

                        ContextMenuEngine.SaveModernItems(_modernItems.ToList());
                        string packageFolder = ContextMenuEngine.GetModernPackageFolder();
                        await ContextMenuEngine.RegisterSparsePackageAsync(packageFolder);
                    }

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
                        Title = ResourceString.GetString("cme_import_success_title") ?? "Import Successful",
                        Content = ResourceString.GetString("cme_import_success_desc") ?? "Your context menu configuration has been successfully imported and applied to Windows!",
                        CloseButtonText = ResourceString.GetString("cme_ok") ?? "OK",
                        XamlRoot = RootGrid.XamlRoot
                    };

                    await successDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = ResourceString.GetString("cme_import_failed_title") ?? "Import Failed",
                    Content = $"{ResourceString.GetString("cme_import_failed_desc") ?? "An error occurred while importing your configuration file:"}\n\n{ex.Message}",
                    CloseButtonText = ResourceString.GetString("cme_close") ?? "Close",
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

        private async void ModernMenuToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            bool enableModern = ModernMenuToggle.IsOn;
            ModernMenuToggle.IsEnabled = false;

            try
            {
                string packageFolder = ContextMenuEngine.GetModernPackageFolder();

                if (enableModern)
                {
                    ContentDialog confirmDialog = new ContentDialog
                    {
                        Title = ResourceString.GetString("cme_reboot_title") ?? "Application Restart Required",
                        Content = ResourceString.GetString("cme_reboot_desc") ?? "To properly apply the modern context menu extensions, the application needs to restart. Would you like to continue?",
                        PrimaryButtonText = ResourceString.GetString("cme_continue") ?? "Continue",
                        CloseButtonText = ResourceString.GetString("cme_cancel") ?? "Cancel",
                        XamlRoot = RootGrid.XamlRoot
                    };

                    ContentDialogResult result = await confirmDialog.ShowAsync();

                    if (result != ContentDialogResult.Primary)
                    {
                        ModernMenuToggle.Toggled -= ModernMenuToggle_Toggled;
                        ModernMenuToggle.IsOn = false;
                        ModernMenuToggle.Toggled += ModernMenuToggle_Toggled;
                        ModernMenuToggle.IsEnabled = true;
                        return;
                    }

                    LocalMachineSettingsEngine.IsModernContextMenuEnabled = true;

                    this.Close();
                    SettingsEngine.SelfReboot();
                }
                else
                {
                    LocalMachineSettingsEngine.IsModernContextMenuEnabled = false;
                    await ContextMenuEngine.UnregisterSparsePackageAsync(packageFolder);
                    ModernMenuToggle.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ContextMenuEditor] Failed to toggle modern menu: {ex.Message}");
                ModernMenuToggle.IsEnabled = true;
            }
        }

        private void BrowseExe_Click(object sender, RoutedEventArgs e)
        {
            IntPtr pFile = Marshal.AllocHGlobal(260 * Marshal.SystemDefaultCharSize);
            try
            {
                Marshal.WriteInt16(pFile, 0);

                var ofn = new OPENFILENAME();
                ofn.lStructSize = Marshal.SizeOf(typeof(OPENFILENAME));
                ofn.hwndOwner = WindowNative.GetWindowHandle(this);
                ofn.lpstrFilter = "Executables (*.exe;*.bat;*.cmd)\0*.exe;*.bat;*.cmd\0All Files (*.*)\0*.*\0";
                ofn.lpstrFile = pFile;
                ofn.nMaxFile = 260;
                ofn.lpstrTitle = ResourceString.GetString("cme_select_executable") ?? "Select Executable";
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
                ofn.hwndOwner = WindowNative.GetWindowHandle(this);
                ofn.lpstrFilter = "Icons (*.ico;*.dll;*.exe)\0*.ico;*.dll;*.exe\0All Files (*.*)\0*.*\0";
                ofn.lpstrFile = pFile;
                ofn.nMaxFile = 260;
                ofn.lpstrTitle = ResourceString.GetString("cme_select_icon") ?? "Select Icon";
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
            if (string.IsNullOrWhiteSpace(TitleInput.Text)) return;
            if (string.IsNullOrWhiteSpace(ExePathInput.Text)) return;

            string targetStr = TargetInput.SelectedIndex switch
            {
                1 => "Folders",
                2 => "Background",
                _ => "Files"
            };

            string finalExe = ExePathInput.Text;
            string finalArgs = ArgsInput.Text;
            string finalIcon = IconPathInput != null && !string.IsNullOrWhiteSpace(IconPathInput.Text)
                ? IconPathInput.Text
                : ExePathInput.Text;

            bool runAsAdmin = RunAsAdminToggle != null && RunAsAdminToggle.IsOn;
            bool hiddenWindow = HiddenWindowToggle != null && HiddenWindowToggle.IsOn;
            bool isExtended = ExtendedToggle != null && ExtendedToggle.IsOn;
            string specificExt = SpecificExtInput != null ? SpecificExtInput.Text.Trim() : "*";
            if (string.IsNullOrEmpty(specificExt)) specificExt = "*";

            string positionStr = PositionInput?.SelectedIndex switch
            {
                1 => "Top",
                2 => "Bottom",
                _ => "Default"
            };

            if (runAsAdmin || hiddenWindow)
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
                    IsSubMenu = false
                };

                _modernItems.Add(newItem);
                ContextMenuEngine.SaveModernItems(_modernItems.ToList());

                string packageFolder = ContextMenuEngine.GetModernPackageFolder();
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
                    IsSubMenu = false
                };

                _classicItems.Add(newItem);
                ContextMenuEngine.AddClassicItem(newItem);
            }

            TitleInput.Text = string.Empty;
            ExePathInput.Text = string.Empty;
            ArgsInput.Text = string.Empty;

            if (IconPathInput != null) IconPathInput.Text = string.Empty;
            if (RunAsAdminToggle != null) RunAsAdminToggle.IsOn = false;
            if (HiddenWindowToggle != null) HiddenWindowToggle.IsOn = false;
            if (ExtendedToggle != null) ExtendedToggle.IsOn = false;
            if (SpecificExtInput != null) SpecificExtInput.Text = string.Empty;
            if (PositionInput != null) PositionInput.SelectedIndex = 0;
            if (QuickTemplatesComboBox != null) QuickTemplatesComboBox.SelectedIndex = -1;
        }

        private async void AddSeparator_Click(object sender, RoutedEventArgs e)
        {
            if (MenuTypeSelector.SelectedIndex != 0) return;

            TextBox inputTextBox = new TextBox
            {
                PlaceholderText = "e.g., Developer Tools",
                Width = 350,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            ContentDialog nameDialog = new ContentDialog
            {
                Title = ResourceString.GetString("cme_separator_dialog_title") ?? "Name Your Separator",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = ResourceString.GetString("cme_separator_dialog_desc") ?? "Give this separator a label to easily identify it in your list. (Note: Windows will only draw a physical line in the actual menu).",
                            TextWrapping = TextWrapping.Wrap
                        },
                        inputTextBox
                    }
                },
                PrimaryButtonText = ResourceString.GetString("cme_add") ?? "Add",
                CloseButtonText = ResourceString.GetString("cme_cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot
            };

            ContentDialogResult result = await nameDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string rawText = string.IsNullOrWhiteSpace(inputTextBox.Text)
                    ? "Separator" :
                    inputTextBox.Text.Trim();

                string customTitle = $"──────── {rawText} ────────";

                string targetStr = TargetInput.SelectedIndex switch
                {
                    1 => "Folders",
                    2 => "Background",
                    _ => "Files"
                };

                var separatorItem = new ModernContextMenuItem
                {
                    Title = customTitle,
                    IsSeparator = true,
                    Target = targetStr,
                    ExePath = "separator",
                    Position = "Default"
                };

                _modernItems.Add(separatorItem);
                ContextMenuEngine.SaveModernItems(_modernItems.ToList());

                string packageFolder = ContextMenuEngine.GetModernPackageFolder();
                await ContextMenuEngine.RegisterSparsePackageAsync(packageFolder);
            }
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

        private async void Expander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            await Task.Delay(50);

            sender.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = true
            });
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

        #region Custom Drag & Drop Logic

        private UIElement? _activeDraggedCard = null;
        private ListViewItem? _activeDraggedItem = null;
        private ListViewItem? _hoveredTargetItem = null;
        private bool _isTrackingDrag = false;
        private Windows.Foundation.Point _dragStartPoint;
        private Windows.Foundation.Point _draggedItemBasePos;
        private Dictionary<ListViewItem, Windows.Foundation.Rect> _logicalBounds = new();

        private void ContextCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid card && !_isTrackingDrag)
            {
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorTertiaryBrush", out object tertiaryBrush))
                    card.Background = (Brush)tertiaryBrush;
            }
        }

        private void ContextCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid card && !_isTrackingDrag)
            {
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out object secBrush))
                    card.Background = (Brush)secBrush;
            }
        }

        private void ContextCard_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid card)
            {
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorTertiaryBrush", out object tertiaryBrush))
                    card.Background = (Brush)tertiaryBrush;

                var dataItem = card.DataContext;
                var container = ItemsListView.ContainerFromItem(dataItem) as ListViewItem;

                if (container != null)
                {
                    _activeDraggedCard = card;
                    _activeDraggedItem = container;
                    _hoveredTargetItem = null;
                    _isTrackingDrag = false;
                    _dragStartPoint = e.GetCurrentPoint(ItemsListView).Position;

                    _logicalBounds.Clear();
                    foreach (var item in ItemsListView.Items)
                    {
                        if (ItemsListView.ContainerFromItem(item) is ListViewItem lvi)
                        {
                            var transform = lvi.TransformToVisual(ItemsListView);
                            var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, lvi.ActualWidth, lvi.ActualHeight));
                            _logicalBounds[lvi] = bounds;

                            if (lvi.ContentTemplateRoot is UIElement rootElement)
                            {
                                rootElement.TranslationTransition = new Microsoft.UI.Xaml.Vector3Transition { Duration = TimeSpan.FromMilliseconds(250) };
                            }
                        }
                    }

                    if (_logicalBounds.TryGetValue(container, out var draggedBounds))
                    {
                        _draggedItemBasePos = new Windows.Foundation.Point(draggedBounds.X, draggedBounds.Y);
                    }

                    Canvas.SetZIndex(container, 1000);
                    card.CapturePointer(e.Pointer);
                }
            }
        }

        private void ContextCard_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_activeDraggedCard == null || _activeDraggedItem == null) return;

            var currentPoint = e.GetCurrentPoint(ItemsListView).Position;
            double deltaX = currentPoint.X - _dragStartPoint.X;
            double deltaY = currentPoint.Y - _dragStartPoint.Y;

            if (!_isTrackingDrag && (Math.Abs(deltaX) > 4 || Math.Abs(deltaY) > 4))
            {
                _isTrackingDrag = true;
                if (_activeDraggedCard is UIElement uiCard)
                    uiCard.TranslationTransition = null;
            }

            if (_isTrackingDrag)
            {
                if (_activeDraggedCard is UIElement uiCard)
                {
                    uiCard.Translation = new System.Numerics.Vector3(0, (float)deltaY, 10f);
                    uiCard.Opacity = 0.8f;
                }

                ListViewItem? newHoveredItem = null;

                foreach (var kvp in _logicalBounds)
                {
                    if (kvp.Key == _activeDraggedItem) continue;

                    if (kvp.Value.Contains(currentPoint))
                    {
                        newHoveredItem = kvp.Key;
                        break;
                    }
                }

                if (newHoveredItem != _hoveredTargetItem)
                {
                    if (_hoveredTargetItem != null && _hoveredTargetItem.ContentTemplateRoot is UIElement oldElement)
                    {
                        oldElement.Translation = System.Numerics.Vector3.Zero;
                    }

                    _hoveredTargetItem = newHoveredItem;

                    if (_hoveredTargetItem != null && _hoveredTargetItem.ContentTemplateRoot is UIElement targetElement)
                    {
                        var targetRect = _logicalBounds[_hoveredTargetItem];
                        float offsetY = (float)(_draggedItemBasePos.Y - targetRect.Y);
                        targetElement.Translation = new System.Numerics.Vector3(0, offsetY, 0);
                    }
                }
            }
        }

        private async void ContextCard_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid card)
            {
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out object secBrush))
                    card.Background = (Brush)secBrush;

                card.ReleasePointerCapture(e.Pointer);
            }

            if (_isTrackingDrag && _activeDraggedItem != null)
            {
                foreach (var item in ItemsListView.Items)
                {
                    if (ItemsListView.ContainerFromItem(item) is ListViewItem lvi)
                    {
                        if (lvi.ContentTemplateRoot is UIElement rootElement)
                        {
                            rootElement.TranslationTransition = null;
                            rootElement.Translation = System.Numerics.Vector3.Zero;
                            rootElement.Opacity = 1.0f;
                        }
                        Canvas.SetZIndex(lvi, 0);
                    }
                }

                if (_hoveredTargetItem != null && _hoveredTargetItem != _activeDraggedItem)
                {
                    var originalTransitions = ItemsListView.ItemContainerTransitions;
                    ItemsListView.ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();

                    var draggedData = ItemsListView.ItemFromContainer(_activeDraggedItem);
                    var targetData = ItemsListView.ItemFromContainer(_hoveredTargetItem);

                    int oldIndex = ItemsListView.Items.IndexOf(draggedData);
                    int newIndex = ItemsListView.Items.IndexOf(targetData);

                    if (oldIndex != -1 && newIndex != -1)
                    {
                        if (MenuTypeSelector.SelectedIndex == 0) // Modern Menu
                        {
                            _modernItems.Move(oldIndex, newIndex);
                            ContextMenuEngine.SaveModernItems(_modernItems.ToList());
                            await ContextMenuEngine.RegisterSparsePackageAsync(ContextMenuEngine.GetModernPackageFolder());
                        }
                        else // Classic Menu
                        {
                            _classicItems.Move(oldIndex, newIndex);
                            var currentItems = ContextMenuEngine.GetClassicItems();
                            foreach (var oldI in currentItems) ContextMenuEngine.RemoveClassicItem(oldI);
                            foreach (var newI in _classicItems) ContextMenuEngine.AddClassicItem(newI);
                        }
                    }

                    ItemsListView.UpdateLayout();
                    if (originalTransitions != null)
                        ItemsListView.ItemContainerTransitions = originalTransitions;
                }
            }
            else if (_activeDraggedCard != null)
            {
                if (_activeDraggedCard is UIElement uiCard)
                {
                    uiCard.TranslationTransition = null;
                    uiCard.Translation = System.Numerics.Vector3.Zero;
                    uiCard.Opacity = 1.0f;
                }
                if (_activeDraggedItem != null) Canvas.SetZIndex(_activeDraggedItem, 0);
            }

            _activeDraggedCard = null;
            _activeDraggedItem = null;
            _hoveredTargetItem = null;
            _isTrackingDrag = false;
        }

        private void ContextCard_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            ContextCard_PointerReleased(sender, e);
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
            0 => ResourceString.GetString("cme_target_files") ?? "Files",
            1 => ResourceString.GetString("cme_target_folders") ?? "Folders",
            2 => ResourceString.GetString("cme_target_background") ?? "Background",
            _ => ResourceString.GetString("cme_target_unknown") ?? "Unknown"
        };
    }

    public class CleanerItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> TargetPaths { get; set; } = new();

        public string TargetPathDisplay => TargetPaths.Count > 1
            ? string.Format(ResourceString.GetString("cme_applied_to_locations") ?? "Applied to {0} registry locations", TargetPaths.Count)
            : TargetPaths.FirstOrDefault() ?? "";

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

    #region Converter

    public class InverseBoolToVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isSeparator && isSeparator)
                return Visibility.Collapsed;

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    #endregion
}