// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Input;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Pages;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Windowing;
using Windows.System;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class MainWinViewModel : ViewModelBase
    {
        #region Native API (P/Invoke)
        [System.Runtime.InteropServices.DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);
        #endregion

        #region Fields
        private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();

        private string _currentViewTag = "Home";
        public string PreviousViewTag { get; private set; } = "Home";

        public string CurrentViewTag
        {
            get => _currentViewTag;
            set
            {
                if (_currentViewTag != value)
                {
                    PreviousViewTag = _currentViewTag;

                    _currentViewTag = value;
                    OnPropertyChanged();
                }
            }
        }

        private ImageSource? _displayProfileAvatar;
        private string? _displayProfileName;
        private bool _isNeedUpdate;
        private bool _isOverlayVisible;

        private DateTime _lastNavTime = DateTime.MinValue;

        private bool _isWindowVisible = !App.IsStartedHidden;

        public string AssignedUserType => UserSession.UserType ?? "Guest";

        public IEnumerable<VirtualKeyModifiers> AvailableModifiers { get; } = new[]
        {
            VirtualKeyModifiers.Control,
            VirtualKeyModifiers.Menu,
            VirtualKeyModifiers.Shift,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu
        };

        public IEnumerable<VirtualKey> AvailableKeys { get; } = Enumerable.Range((int)VirtualKey.A, 26)
            .Select(k => (VirtualKey)k)
            .Concat(Enumerable.Range((int)VirtualKey.F1, 12).Select(k => (VirtualKey)k));
        #endregion

        #region Events
        public static event Action? AppHidden;
        public static event Action? AppRestored;
        public static event Action<ImageSource?>? UserProfileUpdated;

        public static void NotifyUserProfileUpdated(ImageSource? newImage) => UserProfileUpdated?.Invoke(newImage);
        #endregion

        #region Properties

        public bool IsAdmin
        {
            get
            {
                return new WindowsPrincipal(WindowsIdentity.GetCurrent())
                    .IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public bool UseHotkey
        {
            get => LocalMachineSettingsEngine.UseHotkey;
            set
            {
                if (LocalMachineSettingsEngine.UseHotkey != value)
                {
                    LocalMachineSettingsEngine.UseHotkey = value;
                    OnPropertyChanged();
                    _ = App.NotifyHotkeySettingsChanged();
                }
            }
        }

        public VirtualKeyModifiers OptimizationModifiers
        {
            get => LocalMachineSettingsEngine.OptimizationModifiers;
            set
            {
                if (value != LocalMachineSettingsEngine.OptimizationModifiers)
                {
                    LocalMachineSettingsEngine.OptimizationModifiers = value;
                    OnPropertyChanged();
                    _ = App.NotifyHotkeySettingsChanged();
                }
            }
        }

        public VirtualKey OptimizationKey
        {
            get => LocalMachineSettingsEngine.OptimizationKey;
            set
            {
                if (value != LocalMachineSettingsEngine.OptimizationKey)
                {
                    LocalMachineSettingsEngine.OptimizationKey = value;
                    OnPropertyChanged();
                    _ = App.NotifyHotkeySettingsChanged();
                }
            }
        }



        public bool IsOverlayVisible
        {
            get => _isOverlayVisible;
            set
            {
                if (_isOverlayVisible != value)
                {
                    _isOverlayVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public ImageSource? DisplayProfileAvatar
        {
            get
            {
                if (_displayProfileAvatar == null)
                {
                    if (UserSession.ProfileImage != null)
                    {
                        _displayProfileAvatar = UserSession.ProfileImage;
                    }
                    else
                    {
                        _displayProfileAvatar = _systemDiagnostics.GetProfileImage();
                    }
                }
                return _displayProfileAvatar;
            }
        }

        public string DisplayProfileName
        {
            get
            {
                if (string.IsNullOrEmpty(_displayProfileName))
                {
                    _displayProfileName = _systemDiagnostics.GetProfileName();
                }
                return _displayProfileName;
            }
        }

        public bool IsNeedUpdate
        {
            get => _isNeedUpdate;
            set
            {
                _isNeedUpdate = value;
                OnPropertyChanged();
            }
        }

        public bool IsRunOnStartUp
        {
            get => SettingsEngine.IsRunOnStartUp;
            set
            {
                if (SettingsEngine.IsRunOnStartUp != value)
                {
                    SettingsEngine.IsRunOnStartUp = value;
                    OnPropertyChanged(nameof(IsRunOnStartUp));
                }
            }
        }

        public bool IsStartMinimized
        {
            get => SettingsEngine.IsStartMinimized;
            set
            {
                if (SettingsEngine.IsStartMinimized != value)
                {
                    SettingsEngine.IsStartMinimized = value;
                    OnPropertyChanged(nameof(IsStartMinimized));
                }
            }
        }

        public Visibility IsWindowBorderVisible
        {
            get => SettingsEngine.IsWindowBorderEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool IsWindowBorderEnabled
        {
            get => SettingsEngine.IsWindowBorderEnabled;
            set
            {
                if (SettingsEngine.IsWindowBorderEnabled != value)
                {
                    SettingsEngine.IsWindowBorderEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsWindowBorderVisible));
                }
            }
        }

        public bool IsLowPriorityEnabled
        {
            get => DiagnosticsPageViewModel.Current.RunOnLowPriority;
        }

        public string DisplayTweakVersion =>
            (Assembly.GetEntryAssembly() ?? throw new InvalidOperationException())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown Version";
        #endregion

        #region Commands
        public RelayCommand<string> ExecuteNavigateCommand { get; }

        public ICommand MaximizeCommand { get; }
        public ICommand MinimizeCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ToggleWindowVisibilityCommand { get; }
        public ICommand ToggleRunOnStartupCommand { get; }
        public ICommand ToggleLowPriorityCommand { get; }
        public ICommand OpenSecurityCommand { get; }
        public ICommand OpenMaintenanceCommand { get; }
        public ICommand OpenStartupAppsCommand { get; }
        public ICommand OpenDnsCommand { get; }
        #endregion

        #region Constructor
        public MainWinViewModel()
        {
            ExecuteNavigateCommand = new RelayCommand<string>(ExecuteNavigate);

            MaximizeCommand = new RelayCommand<object>(_ => ExecuteMaximize());
            MinimizeCommand = new RelayCommand<object>(_ => ExecuteMinimize());
            CloseCommand = new RelayCommand<object>(_ => ExecuteClose());
            OpenSecurityCommand = new RelayCommand<object>(_ => OpenPageFromTray("Diagnostics", "Security"));
            OpenMaintenanceCommand = new RelayCommand<object>(_ => OpenPageFromTray("Diagnostics", "Maintenance"));
            OpenStartupAppsCommand = new RelayCommand<object>(_ => OpenPageFromTray("SystemManager", "StartupManagerPage"));
            OpenDnsCommand = new RelayCommand<object>(_ => OpenPageFromTray("Diagnostics", "DnsCrypt"));
            ToggleWindowVisibilityCommand = new RelayCommand<object>(_ => ExecuteToggleVisibility());

            LocalizationService.Instance.PropertyChanged += OnLocalizationPropertyChanged;

            UserProfileUpdated += OnUserProfileUpdated;

            ToggleRunOnStartupCommand = new RelayCommand(_ =>
            {
                IsRunOnStartUp = !IsRunOnStartUp;
            });

            LocalMachineSettingsEngine.SettingChanged += (sender, settingKey) =>
            {
                if (settingKey == "RunOnPriority")
                {
                    OnPropertyChanged(nameof(IsLowPriorityEnabled));
                }
            };

            ToggleLowPriorityCommand = new RelayCommand(_ =>
            {
                bool isCurrentlyLow = LocalMachineSettingsEngine.RunOnPriority == Enums.Priority.Low;
                LocalMachineSettingsEngine.RunOnPriority = isCurrentlyLow ? Enums.Priority.Normal : Enums.Priority.Low;

                App.SetPriority(LocalMachineSettingsEngine.RunOnPriority);
            });

            ExecuteNavigate("Home");
        }

        private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Item[]")
            {
                OnPropertyChanged(string.Empty);
            }
        }
        #endregion

        #region Methods
        private void ExecuteNavigate(string? tag)
        {
            if (string.IsNullOrEmpty(tag)) return;

            if ((DateTime.Now - _lastNavTime).TotalMilliseconds < 300) return;
            _lastNavTime = DateTime.Now;

            CurrentViewTag = tag;

            UpdatePowerState(tag);
        }

        private void OnUserProfileUpdated(ImageSource? newImage)
        {
            App.UIThreadDispatcher?.TryEnqueue(() =>
            {
                if (newImage != null)
                {
                    _displayProfileAvatar = newImage;
                }
                else
                {
                    // Fallback just in case
                    _displayProfileAvatar = null;
                }

                _displayProfileName = null;

                OnPropertyChanged(nameof(DisplayProfileAvatar));
                OnPropertyChanged(nameof(DisplayProfileName));
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LocalizationService.Instance.PropertyChanged -= OnLocalizationPropertyChanged;
            }

            base.Dispose(disposing);
        }

        private void ExecuteMaximize()
        {
            var window = App.MainWindow;
            if (window == null) return;

            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            appWindow.Show();
            window.Activate();
            _isWindowVisible = true;

            DiagnosticsPageViewModel.Current.StopBackgroundGuardian();

            DiagnosticsPageViewModel.Current.SetMemoryThreshold(450);

            if (CurrentViewTag == "Home" ||
                CurrentViewTag == "Diagnostics" ||
                CurrentViewTag == "SystemManager" ||
                CurrentViewTag == "Software" ||
                CurrentViewTag == "RegistryEditor")
            {
                EfficiencyModeHelper.IsUIWakeLockActive = true;
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);
                Debug.WriteLine($"[Restore] Performance Zone: {CurrentViewTag}. Wake Lock RAISED.");
            }
            else
            {
                EfficiencyModeHelper.IsUIWakeLockActive = false;
                if (LocalMachineSettingsEngine.RunOnPriority == Enums.Priority.Low)
                {
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
                }
                Debug.WriteLine($"[Restore] Efficiency Zone: {CurrentViewTag}. Wake Lock DROPPED.");
            }

            AppRestored?.Invoke();

            DiagnosticsPageViewModel.Current.ResumeUiUpdates();
        }

        private void ExecuteMinimize()
        {
            var window = App.MainWindow;
            if (window == null) return;

            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Hide();
            _isWindowVisible = false;

            DiagnosticsPageViewModel.Current.StartBackgroundGuardian();

            EfficiencyModeHelper.IsUIWakeLockActive = false;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);

            if (LocalMachineSettingsEngine.RunOnPriority == Enums.Priority.Low)
            {
                DiagnosticsPageViewModel.Current.SetMemoryThreshold(350);
            }
            else
            {
                DiagnosticsPageViewModel.Current.SetMemoryThreshold(450);
            }

            AppHidden?.Invoke();

            DiagnosticsPageViewModel.Current.PauseUiUpdates();

            Task.Run(() =>
            {
                DiagnosticsPageViewModel.Current.ForceImmediateMemoryCleanup();
                Debug.WriteLine("[TrayMinimize] App minimized. Guardian flushed RAM.");
            });
        }

        private void ExecuteToggleVisibility()
        {
            if (_isWindowVisible)
            {
                ExecuteMinimize();
            }
            else
            {
                ExecuteMaximize();
            }
        }

        private void OpenPageFromTray(string tag, string requestedPane = "")
        {
            if (tag == "Diagnostics" && !string.IsNullOrEmpty(requestedPane))
            {
                if (DiagnosticsPage.ExternalPaneRequest != null)
                    DiagnosticsPage.ExternalPaneRequest.Invoke(requestedPane);
                else
                    DiagnosticsPage.RequestedPaneOnLoad = requestedPane;
            }

            if (tag == "SystemManager" && !string.IsNullOrEmpty(requestedPane))
            {
                if (SystemManagerPage.ExternalPaneRequest != null)
                    SystemManagerPage.ExternalPaneRequest.Invoke(requestedPane);
                else
                    SystemManagerPage.RequestedPaneOnLoad = requestedPane;
            }

            if (tag == "Software" && !string.IsNullOrEmpty(requestedPane))
            {
                if (SoftwareCenterPage.ExternalPaneRequest != null)
                    SoftwareCenterPage.ExternalPaneRequest.Invoke(requestedPane);
                else
                    SoftwareCenterPage.RequestedPaneOnLoad = requestedPane;
            }

            CurrentViewTag = tag;

            ExecuteMaximize();
            ExecuteNavigate(tag);
        }

        public void UpdatePowerState(string tag)
        {
            // Efficiency Mode should be OFF
            if (tag == "Home" ||
                tag == "Diagnostics" ||
                tag == "SystemManager" ||
                tag == "Software" ||
                tag == "SystemCleaner" ||
                tag == "RegistryEditor" ||
                tag == "Optimize" ||
                tag == "Customize" ||
                tag == "ProfileBuilder")
            {
                EfficiencyModeHelper.IsUIWakeLockActive = true;
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);
                Debug.WriteLine($"[Power] HIGH PERFORMANCE: {tag}");
            }
            else
            {
                EfficiencyModeHelper.IsUIWakeLockActive = false;
                if (LocalMachineSettingsEngine.RunOnPriority == Enums.Priority.Low)
                {
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
                }
                Debug.WriteLine($"[Power] EFFICIENCY MODE: {tag}");
            }
        }

        private void ExecuteClose()
        {
            App.ExitApp();
        }
        #endregion
    }
}