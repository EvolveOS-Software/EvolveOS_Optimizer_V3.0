using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class ScriptsPage : Page
    {
        private TimerControlManager? _timer = default;
        
        public ScriptsViewModel ViewModel { get; } = new();

        public ScriptsPage()
        {
            this.InitializeComponent();
            this.DataContext = ViewModel;

            UIHelper.RegisterPageTransition(RootGrid, this);

            ViewModel.OnScriptsUpdated += UpdateEmptyState;

            this.Unloaded += (s, e) => {
                _timer?.Stop();
                ViewModel.OnScriptsUpdated -= UpdateEmptyState;
            };
            
            this.Loaded += (s, e) => { 
                InitializeTimer();
                UpdateEmptyState();
                EmptyStateAnimation.Begin();
            };
        }

        private void InitializeTimer()
        {
            _timer = new TimerControlManager(TimeSpan.Zero, TimerControlManager.TimerMode.CountUp, time =>
            {
                if ((int)time.TotalSeconds % 5 == 0)
                {
                    this.DispatcherQueue.TryEnqueue(async () =>
                    {
                        if (ViewModel.RefreshScriptsCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand asyncCmd)
                        {
                            await asyncCmd.ExecuteAsync(null);
                        }
                    });
                }
            });
        }

        private void UpdateEmptyState()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.FilteredScripts != null && ViewModel.FilteredScripts.Count > 0)
                    VisualStateManager.GoToState(this, "HasScripts", true);
                else
                    VisualStateManager.GoToState(this, "NoScripts", true);
            });
        }

        private void FileCard_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (ViewModel.IsMultiSelectMode) return;

            if (sender is Microsoft.UI.Xaml.FrameworkElement element && element.DataContext is Core.Model.ScriptsModel script)
            {
                if (ViewModel.RunSingleScriptCommand.CanExecute(script))
                {
                    ViewModel.RunSingleScriptCommand.Execute(script);
                }
            }
        }
    }
}