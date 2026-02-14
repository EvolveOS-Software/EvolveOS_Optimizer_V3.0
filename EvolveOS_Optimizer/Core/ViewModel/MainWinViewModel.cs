using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Xaml.Media;
using System.Reflection;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class MainWinViewModel : ViewModelBase
    {
        private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();

        #region Properties

        private string _currentViewTag = "Home";
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

        private ImageSource? _displayProfileAvatar;
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

        private string? _displayProfileName;
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

        private bool _isNeedUpdate;
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

        public RelayCommand<string> ExecuteNavigateCommand { get; }

        public MainWinViewModel()
        {
            ExecuteNavigateCommand = new RelayCommand<string>(ExecuteNavigate);

            LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]") OnPropertyChanged(string.Empty);
            };

            ExecuteNavigate("Home");
        }

        private void ExecuteNavigate(string? tag)
        {
            if (string.IsNullOrEmpty(tag)) return;

            CurrentViewTag = tag;
        }
    }
}