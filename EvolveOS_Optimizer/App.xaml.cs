using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.UI.Xaml;

namespace EvolveOS_Optimizer
{
    public partial class App : Application
    {
        public Window? MainWindow { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();

            SettingsEngine.CheckingParameters();

            MainWindow.Activate();
        }
    }
}
