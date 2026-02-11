using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Services;
using System.Reflection;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class MainWinViewModel : ViewModelBase
    {
        private readonly SystemDiagnostics _systemDiagnostics = new SystemDiagnostics();

        #region Properties
        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            set
            {
                if (_currentView != value)
                {
                    _currentView = value;
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

            var newPage = tag switch
            {
                "Home" => (Page)new Pages.HomePage(),
                "Security" => new Pages.SecurityPage(),
                /*"Utils" => new Pages.UtilitiesPage(),
                "Confidentiality" => new Pages.PrivacyPage(),
                "Interface" => new Pages.InterfacePage(),*/
                "Software" => new Pages.SoftwareCenterPage(),
                "GroupPolicy" => new Pages.GroupPolicyPage(),
                //"Services" => new Pages.ServicesPage(),
                //"System" => new Pages.SystemPage(),
                "Settings" => new Pages.SettingsPage(),
                _ => new Pages.HomePage()
            };
            CurrentView = newPage;
        }
    }
}
