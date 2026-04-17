// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Tweaks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Pages
{
    public partial class PackagesPage : Page, IPurgeable, IBusyPage
    {
        #region Properties & State Fields
        private readonly Dictionary<string, string> _currentCardStates = new();

        private CancellationTokenSource? _pageCts;

        private TimerControlManager? _timer = default;
        private readonly BackgroundQueue _backgroundQueue = new BackgroundQueue();
        private readonly UninstallingPackages _uninstalling = new UninstallingPackages();
        private bool _isWebViewRemoval = false;

        private Queue<FrameworkElement> _entranceQueue = new Queue<FrameworkElement>();
        private DispatcherTimer? _staggerTimer;

        private int _cardsLoadedCount = 0;
        private bool _isEntranceAnimationActive = true;
        public bool IsStaggeredEntranceEnabled { get; set; } = false;

        private bool _isHoveringItem = false;
        private bool _isUpdating = false;

        #region Navigation Guard
        public bool IsBusy { get; private set; }
        public string BusyTitle => ResourceString.GetString("dialog_uninstall_in_progress_title") ?? "Uninstallation in Progress";
        public string BusyMessage => ResourceString.GetString("dialog_uninstall_in_progress_content") ?? "Leaving this tab will cancel the removal process. Proceed?";
        #endregion

        #endregion

        #region Constructor & Lifecycle
        public PackagesPage()
        {
            InitializeComponent();

            this.Loaded += PackagesPage_Loaded;
            this.Unloaded += PackagesPage_Unloaded;
        }

        private void PackagesPage_Loaded(object sender, RoutedEventArgs e)
        {
            _pageCts?.Cancel();
            _pageCts?.Dispose();
            _pageCts = new CancellationTokenSource();
            var token = _pageCts.Token;

            if (HcPanel != null)
            {
                HcPanel.AnimationFinished = () => ReleaseTypewriter();
            }

            SyncVisualStates();

            _timer = new TimerControlManager(TimeSpan.FromSeconds(5), TimerControlManager.TimerMode.CountUp, async time =>
            {
                if (!this.IsLoaded || token.IsCancellationRequested || _isUpdating)
                {
                    return;
                }

                _isUpdating = true;

                try
                {
                    await Task.Run(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        _uninstalling.GetInstalledPackages();

                        this.DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.Low, () =>
                        {
                            if (!this.IsLoaded || token.IsCancellationRequested) return;

                            UninstallingPackages.OnPackagesChanged();
                            SyncVisualStates();
                        });
                    }, token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PackagesPage] Timer Loop Error: {ex.Message}");
                }
                finally
                {
                    _isUpdating = false;
                }
            });

            _timer.Start();
        }

        private void PackagesPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Purge();
        }
        #endregion

        #region UI Event Handlers (Buttons & Menus)

        private async void ToggleButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!this.IsLoaded) return;

            if (sender is FrameworkElement fe && fe.DataContext is PackagesModel model)
            {
                var vm = this.DataContext as PackagesViewModel;

                if (vm != null && vm.IsMultiSelectMode)
                {
                    if (model.Installed == false) return;

                    e.Handled = true;
                    vm.ToggleSelection(model);

                    var card = UIHelper.FindParent<ContentControl>(fe);
                    if (card != null)
                    {
                        VisualStateManager.GoToState(card, model.IsSelected ? "Selected" : "Unselected", false);
                    }
                    return;
                }

                e.Handled = true;

                string packageName = model.Name;
                bool isInstalled = model.Installed == true;

                if (!isInstalled && packageName == "OneDrive")
                {
                    await HandleOneDriveRestore(packageName);
                }
                else if (isInstalled)
                {
                    if (App.MainWindow is MainWindow mainWindow)
                    {
                        if (packageName == "Edge")
                        {
                            mainWindow.TxtGlobalTitle.Text = ResourceString.GetString("title_over_pkg") ?? "Uninstallation confirmation";
                            mainWindow.TxtGlobalMessage.Text = ResourceString.GetString("text_over_pkg") ?? "Edge Removal Warning...";
                            mainWindow.TxtGlobalQuestion.Text = ResourceString.GetString("question_over_pkg") ?? "Continue deleting Edge + WebView?";
                        }
                        else
                        {
                            mainWindow.TxtGlobalTitle.Text = ResourceString.GetString("title_uninstall") ?? "Confirm Uninstallation";

                            string friendlyAppName = ResourceString.GetString($"{packageName}_pkg");
                            if (string.IsNullOrEmpty(friendlyAppName) || friendlyAppName.StartsWith("[Missing"))
                                friendlyAppName = packageName;

                            string baseMsg = ResourceString.GetString("msg_uninstall_single") ?? "You are about to remove {0}.";
                            mainWindow.TxtGlobalMessage.Text = string.Format(baseMsg, friendlyAppName);
                            mainWindow.TxtGlobalQuestion.Text = ResourceString.GetString("question_uninstall") ?? "Proceed?";
                        }

                        OverlayDialogManager dialogManager = new OverlayDialogManager(
                            mainWindow.GlobalOverlay,
                            mainWindow.BtnGlobalDelete,
                            mainWindow.BtnGlobalCancel);

                        bool confirmed = await dialogManager.Show();

                        if (confirmed)
                        {
                            _isWebViewRemoval = (packageName == "Edge");
                            await HandlePackageRemoval(packageName);
                        }
                    }
                }

                SyncVisualStates();
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            BtnSettings.Flyout?.ShowAt(BtnSettings);
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is PackagesViewModel vm)
            {
                vm.SelectedPackages.Clear();
                foreach (var pkg in vm.DisplayState)
                {
                    if (pkg.Installed == true)
                    {
                        pkg.IsSelected = true;
                        if (!vm.SelectedPackages.Contains(pkg))
                            vm.SelectedPackages.Add(pkg);
                    }
                }

                vm.OnPropertyChanged(nameof(vm.RemoveButtonVisibility));
                SyncVisualStates();
            }
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is PackagesViewModel vm)
            {
                if (vm.DisplayState == null) return;

                foreach (var pkg in vm.DisplayState.ToList())
                {
                    if (pkg.IsSelected)
                    {
                        vm.ToggleSelection(pkg);
                    }
                }

                SyncVisualStates();
            }
        }

        private async void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is PackagesViewModel vm)
            {
                if (vm.SelectedPackages.Count == 0) return;

                if (App.MainWindow is not MainWindow mainWindow) return;

                bool isEdgeSelected = vm.SelectedPackages.Any(p => p.Name == "Edge");

                if (isEdgeSelected)
                {
                    mainWindow.TxtGlobalTitle.Text = ResourceString.GetString("title_over_pkg") ?? "Uninstallation confirmation";
                    mainWindow.TxtGlobalMessage.Text = ResourceString.GetString("text_over_pkg") ?? "Edge Removal Warning...";
                    mainWindow.TxtGlobalQuestion.Text = ResourceString.GetString("question_over_pkg") ?? "Would you like to continue deleting along with EdgeWebView?";
                }
                else
                {
                    mainWindow.TxtGlobalTitle.Text = ResourceString.GetString("title_uninstall") ?? "Confirm Uninstallation";
                    string baseMsg = ResourceString.GetString("msg_uninstall_bulk") ?? "You are about to remove {0} selected packages.";
                    mainWindow.TxtGlobalMessage.Text = string.Format(baseMsg, vm.SelectedPackages.Count);
                    mainWindow.TxtGlobalQuestion.Text = ResourceString.GetString("question_uninstall") ?? "Proceed?";
                }

                OverlayDialogManager dialogManager = new OverlayDialogManager(
                    mainWindow.GlobalOverlay,
                    mainWindow.BtnGlobalDelete,
                    mainWindow.BtnGlobalCancel);

                bool confirmed = await dialogManager.Show();

                if (confirmed)
                {
                    await ExecuteBulkRemoval(vm, isEdgeSelected);
                }
            }
        }
        #endregion

        #region Animations & Staggering

        private async void PackageView_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element)) return;

            if (IsStaggeredEntranceEnabled)
            {
                element.Opacity = 0;
                _entranceQueue.Enqueue(element);

                if (_staggerTimer == null) InitializeStaggerTimer();

                if (_staggerTimer?.IsEnabled == false)
                {
                    _staggerTimer.Start();
                }
            }
            else
            {
                _cardsLoadedCount++;
            }

            if (HcPanel != null)
            {
                await Task.Yield();

                HcPanel.InvalidateMeasure();
                HcPanel.InvalidateArrange();
            }
        }

        private void InitializeStaggerTimer()
        {
            _staggerTimer = new DispatcherTimer();
            _staggerTimer.Interval = TimeSpan.FromMilliseconds(30);
            _staggerTimer.Tick += StaggerTimer_Tick;
        }

        private void StaggerTimer_Tick(object? sender, object e)
        {
            if (_entranceQueue.Count > 0)
            {
                var element = _entranceQueue.Dequeue();
                // FactoryAnimation.AnimateEntrance(element, 0);
            }
            else
            {
                _staggerTimer?.Stop();
            }
        }

        private void Package_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_isEntranceAnimationActive) return;

            if (sender is FrameworkElement animationContainer)
            {
                e.Handled = true;
                _isHoveringItem = true;

                var card = UIHelper.FindParent<ContentControl>(animationContainer);
                if (card != null)
                {
                    Canvas.SetZIndex(card, 100);
                    UpdateDescriptionText(animationContainer);
                }

                FactoryAnimation.AnimateHexagonCardLiftIn(animationContainer);
            }
        }

        private void Package_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement cardGrid)
            {
                _isHoveringItem = false;

                var card = UIHelper.FindParent<ContentControl>(cardGrid);
                if (card != null) Canvas.SetZIndex(card, 0);

                FactoryAnimation.AnimateHexagonCardLiftOut(cardGrid);

                ReleaseTypewriter();
            }
        }

        private async void ReleaseTypewriter()
        {
            if (this.DispatcherQueue == null || !this.IsLoaded) return;

            await Task.Delay(100);

            _isEntranceAnimationActive = false;

            this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                if (!this.IsLoaded) return;

                if (DescBlock != null && !_isHoveringItem)
                {
                    DescBlock.Text = DescBlock.DefaultText;
                }
            });
        }

        #endregion

        #region Helpers & Task Logic

        private void SyncVisualStates()
        {
            if (HcPanel == null || !this.IsLoaded) return;

            foreach (var child in HcPanel.Children)
            {
                if (child is ContentControl card && card.DataContext is PackagesModel model)
                {
                    string targetState = model.IsSelected ? "Selected" : "Unselected";

                    if (_currentCardStates.TryGetValue(model.Name, out string? currentState) && currentState == targetState)
                    {
                        continue;
                    }

                    _currentCardStates[model.Name] = targetState;
                    VisualStateManager.GoToState(card, targetState, true);
                }
            }
        }

        private void UpdateDescriptionText(object sender)
        {
            if (!(sender is FrameworkElement element)) return;

            string packageId = string.Empty;
            if (element.DataContext is PackagesModel model)
            {
                packageId = model.Name;
            }

            if (!string.IsNullOrEmpty(packageId))
            {
                string appName = ResourceString.GetString($"{packageId}_pkg");
                string appDesc = ResourceString.GetString($"{packageId}_desc");

                if (!appName.StartsWith("[Missing Resource:"))
                {
                    DescBlock.Text = $"{appName} • {appDesc}";
                    return;
                }
            }
            DescBlock.Text = string.Empty;
        }

        private async Task HandlePackageRemoval(string packageName)
        {
            var vm = this.DataContext as PackagesViewModel;

            if (_timer != null) _timer.Stop();

            IsBusy = true;

            try
            {
                await _backgroundQueue.QueueTask(async () =>
                {
                    this.DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (this.IsLoaded) UninstallingPackages.HandleAvailabilityStatus(packageName, true);
                    });

                    await UninstallingPackages.RemoveAppxPackage(packageName, _isWebViewRemoval);

                    await Task.Delay(3000);

                    _uninstalling.GetInstalledPackages();

                    this.DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (!this.IsLoaded || vm == null || HcPanel == null)
                        {
                            if (_timer != null) _timer.Start();
                            return;
                        }

                        UninstallingPackages.HandleAvailabilityStatus(packageName, false);

                        if (ExplorerManager.PackageMapping.TryGetValue(packageName, out bool needRestart) && needRestart)
                        {
                            ExplorerManager.Restart();
                        }

                        bool isCurrentlyInstalled = UninstallingPackages.InstalledPackagesCache.Contains(packageName);

                        foreach (var child in HcPanel.Children)
                        {
                            var card = child as ContentControl;
                            if (card != null && card.DataContext is PackagesModel model && model.Name == packageName)
                            {
                                model.Installed = isCurrentlyInstalled;
                                model.IsSelected = false;

                                try { model.OnPropertyChanged(nameof(model.Installed)); } catch { }

                                if (!isCurrentlyInstalled)
                                {
                                    VisualStateManager.GoToState(card, "Unselected", false);
                                    _currentCardStates[model.Name] = "Unselected";

                                    var tb = card.Content as ToggleButton;
                                    if (tb != null)
                                    {
                                        tb.IsChecked = false;
                                        VisualStateManager.GoToState(tb, "Unchecked", true);
                                        VisualStateManager.GoToState(tb, "Normal", true);

                                        var iconImage = UIHelper.FindVisualChildByName<Image>(tb, "Icon");
                                        if (iconImage != null) iconImage.Opacity = 0.4;
                                    }
                                }
                                break;
                            }
                        }

                        UninstallingPackages.OnPackagesChanged();
                        SyncVisualStates();

                        if (_pageCts?.Token.IsCancellationRequested != true)
                        {
                            string friendlyAppName = ResourceString.GetString($"{packageName}_pkg");
                            if (string.IsNullOrEmpty(friendlyAppName) || friendlyAppName.StartsWith("[Missing"))
                                friendlyAppName = packageName;

                            string removedMsg = ResourceString.GetString("notif_pkg_removed") ?? "{0} has been successfully removed.";
                            NotificationManager.Show("info", string.Format(removedMsg, friendlyAppName)).Perform();
                        }

                        if (_timer != null) _timer.Start();
                    });
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteBulkRemoval(PackagesViewModel vm, bool edgeInSelection)
        {
            vm.IsMultiSelectMode = false;
            var packagesToRemove = new List<PackagesModel>(vm.SelectedPackages);
            vm.SelectedPackages.Clear();
            vm.OnPropertyChanged(nameof(vm.RemoveButtonVisibility));

            IsBusy = true;

            try
            {
                SyncVisualStates();

                foreach (var package in packagesToRemove)
                {
                    if (_pageCts?.Token.IsCancellationRequested == true)
                    {
                        await ErrorLogging.LogInfo("[PackagesPage] Bulk removal loop aborted due to navigation cancellation.");
                        break;
                    }

                    string packageName = package.Name;
                    bool removeWebView = edgeInSelection && packageName == "Edge";

                    await _backgroundQueue.QueueTask(async () =>
                    {
                        this.DispatcherQueue?.TryEnqueue(() => { if (this.IsLoaded) UninstallingPackages.HandleAvailabilityStatus(packageName, true); });

                        await UninstallingPackages.RemoveAppxPackage(packageName, removeWebView);
                        await Task.Delay(2000);

                        this.DispatcherQueue?.TryEnqueue(() => { if (this.IsLoaded) UninstallingPackages.HandleAvailabilityStatus(packageName, false); });

                        this.DispatcherQueue?.TryEnqueue(() =>
                        {
                            if (!this.IsLoaded) return;

                            if (ExplorerManager.PackageMapping.TryGetValue(packageName, out bool needRestart) && needRestart)
                            {
                                ExplorerManager.Restart();
                            }
                            UninstallingPackages.OnPackagesChanged();
                            SyncVisualStates();
                        });
                    });
                }

                if (_pageCts?.Token.IsCancellationRequested != true)
                {
                    string notificationMsg = ResourceString.GetString("notif_bulk_completed") ?? "Bulk uninstall Completed";
                    NotificationManager.Show("info", notificationMsg).Perform();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task HandleOneDriveRestore(string packageName)
        {
            await _backgroundQueue.QueueTask(async () =>
            {
                this.DispatcherQueue?.TryEnqueue(() => { if (this.IsLoaded) UninstallingPackages.HandleAvailabilityStatus(packageName, true); });
                await Task.Delay(3000);
                this.DispatcherQueue?.TryEnqueue(() => { if (this.IsLoaded) UninstallingPackages.HandleAvailabilityStatus(packageName, false); });
            });
        }

        private void HandleAnimationChanged(bool isEnabled)
        {
            this.DispatcherQueue?.TryEnqueue(() =>
            {
                if (this.IsLoaded && HcPanel != null) HcPanel.IsAnimationEnabled = isEnabled;
            });
        }

        #region Navigation Guard (IBusyPage)

        public async Task CancelWorkAsync()
        {
            try
            {
                await ErrorLogging.LogInfo("[PackagesPage] Cancellation requested via navigation guard.");
                _pageCts?.Cancel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PackagesPage] Error during cancel: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        #endregion

        #endregion

        #region Purge Page
        public void Purge()
        {
            Debug.WriteLine("[PackagesPage] Purge Initiated...");

            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
                Debug.WriteLine("[PackagesPage] Refresh Timer stopped.");
            }

            if (_staggerTimer != null)
            {
                _staggerTimer.Stop();
                _staggerTimer = null;
                Debug.WriteLine("[PackagesPage] Stagger Timer stopped.");
            }

            if (_pageCts != null)
            {
                try
                {
                    _pageCts.Cancel();
                    _pageCts.Dispose();
                }
                catch (ObjectDisposedException) { }
                _pageCts = null;
                Debug.WriteLine("[PackagesPage] CancellationToken cancelled.");
            }

            _entranceQueue?.Clear();
            _currentCardStates?.Clear();

            if (HcPanel != null)
            {
                HcPanel.AnimationFinished = null;
                HcPanel.Children.Clear();
            }

            if (this.DataContext is IDisposable disposableVM)
            {
                disposableVM.Dispose();
            }
            this.DataContext = null;

            this.Content = null;

            this.Loaded -= PackagesPage_Loaded;
            this.Unloaded -= PackagesPage_Unloaded;

            Debug.WriteLine("[PackagesPage] Purge Complete.");
        }
        #endregion
    }
}