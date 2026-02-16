using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Services;
using System.Reflection;
using System;
using Microsoft.UI.Xaml.Media;

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
        #endregion

        #region Properties

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

            LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]") OnPropertyChanged(string.Empty);
            };

            ExecuteNavigate("Home");
        }
        #endregion

        #region Methods
        private void ExecuteNavigate(string? tag)
        {
            if (string.IsNullOrEmpty(tag)) return;

            CurrentViewTag = tag;
        }
        #endregion
    }
}