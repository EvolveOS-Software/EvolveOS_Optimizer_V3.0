// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License. 

using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class DiagnosticsPage : Page
    {
        public DiagnosticsPageViewModel ViewModel { get; } = new();

        public DiagnosticsPage()
        {
            this.InitializeComponent();
            this.DataContext = ViewModel;

            this.Loaded += DiagnosticsPage_Loaded;
            this.Unloaded += (s, e) =>
            {
                if (DataContext is DiagnosticsPageViewModel vm)
                {
                    vm.Cleanup();
                }
            };
        }

        private async void DiagnosticsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null && !ViewModel.IsScanning)
            {
                await ViewModel.ExecuteFullScanAsync();
            }
        }

        private void HeartbeatScanner_Loaded(object sender, RoutedEventArgs e)
        {
            if (HeartbeatStoryboard != null)
            {
                HeartbeatStoryboard.Begin();
            }
        }

        private async void FixHardwareButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is EvolveOS_Optimizer.Core.Model.HardwareIssue selectedIssue)
            {
                await ViewModel.FixHardwareAsync(selectedIssue);
            }
        }

        private async void StartFullScan_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel != null && !ViewModel.IsScanning)
            {
                await ViewModel.ExecuteFullScanAsync();

                // Optional: Health Sonar to re-calculate after the scan:
                // await CalculateSystemHealthAsync(); 
            }
        }

        private void SystemSonar_Loaded(object sender, RoutedEventArgs e)
        {
            if (SystemSonarStoryboard != null)
            {
                SystemSonarStoryboard.Begin();
            }
        }

        private void SystemSonar_Unloaded(object sender, RoutedEventArgs e)
        {
            if (SystemSonarStoryboard != null)
            {
                SystemSonarStoryboard.Stop();
            }
        }
    }
}