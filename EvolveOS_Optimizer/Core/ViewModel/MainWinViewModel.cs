using EvolveOS_Optimizer.Core.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class MainWinViewModel : ViewModelBase
    {
        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public RelayCommand<string> NavigateCommand { get; }

        public MainWinViewModel()
        {
            NavigateCommand = new RelayCommand<string>(ExecuteNavigate);


            ExecuteNavigate("Settings");
        }

        private void ExecuteNavigate(string? tag)
        {
            if (string.IsNullOrEmpty(tag)) return;

            CurrentView = tag switch
            {
                /*"HomePage" => new Pages.HomePage(),
                "Utils" => new Pages.UtilitiesPage(),
                "Confidentiality" => new Pages.PrivacyPage(),
                "Interface" => new Pages.InterfacePage(),
                "Packages" => new Pages.PackagesPage(),
                "Services" => new Pages.ServicesPage(),
                "System" => new Pages.SystemPage(),*/
                "Settings" => new Pages.SettingsPage(),
                _ => new Pages.SettingsPage()
            };
        }
    }
}
