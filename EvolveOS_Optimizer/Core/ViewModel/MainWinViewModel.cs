using System.ComponentModel;
using System.Reflection;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Services;
using Windows.System;

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

        public string DisplayTweakVersion =>
            (Assembly.GetEntryAssembly() ?? throw new InvalidOperationException())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown Version";
        #endregion

        #region Commands
        public RelayCommand<string> ExecuteNavigateCommand { get; }
        #endregion

        #region Constructor
        public MainWinViewModel()
        {
            ExecuteNavigateCommand = new RelayCommand<string>(ExecuteNavigate);

            LocalizationService.Instance.PropertyChanged += OnLocalizationPropertyChanged;

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
        #endregion
    }
}