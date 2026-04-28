using System.ComponentModel;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Input;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Pages;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Windowing;
using Windows.System;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class MainWinViewModel : ViewModelBase
    {
        #region Fields
        private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();
        private string _currentViewTag = "Home";
        private ImageSource? _displayProfileAvatar;
        private string? _displayProfileName;
        private bool _isNeedUpdate;
        private bool _isOverlayVisible;

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

        #region Properties

        public Visibility IsAdmin
        {
            get
            {
                bool isAdminRole = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                    .IsInRole(WindowsBuiltInRole.Administrator);

                return isAdminRole ? Visibility.Visible : Visibility.Collapsed;
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
                    App.NotifyHotkeySettingsChanged();
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
                    App.NotifyHotkeySettingsChanged();
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
                    App.NotifyHotkeySettingsChanged();
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

        public string CurrentViewTag
        {
            get => _currentViewTag;
            set
            {
                if (_currentViewTag != value)
                {
                    _currentViewTag = value;
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
                    _displayProfileAvatar = _systemDiagnostics.GetProfileImage();
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
            OpenDnsCommand = new RelayCommand<object>(_ => OpenPageFromTray("Utilities"));
            ToggleWindowVisibilityCommand = new RelayCommand<object>(_ => ExecuteToggleVisibility());

            LocalizationService.Instance.PropertyChanged += OnLocalizationPropertyChanged;

            ToggleRunOnStartupCommand = new RelayCommand(_ =>
            {
                IsRunOnStartUp = !IsRunOnStartUp;
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

            CurrentViewTag = tag;
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
                {
                    DiagnosticsPage.ExternalPaneRequest.Invoke(requestedPane);
                }
                else
                {
                    DiagnosticsPage.RequestedPaneOnLoad = requestedPane;
                }
            }

            ExecuteMaximize();
            ExecuteNavigate(tag);
        }

        private void ExecuteClose()
        {
            App.ExitApp();
        }
        #endregion
    }
}