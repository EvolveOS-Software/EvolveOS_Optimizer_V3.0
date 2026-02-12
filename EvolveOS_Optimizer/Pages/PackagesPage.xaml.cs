using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Tweaks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Pages
{
    public partial class PackagesPage : Page
    {
        private readonly Dictionary<string, string> _currentCardStates = new();

        private TimerControlManager? _timer = default;
        private readonly BackgroundQueue _backgroundQueue = new BackgroundQueue();
        private readonly UninstallingPakages _uninstalling = new UninstallingPakages();
        private bool _isWebViewRemoval = false;

        private Queue<FrameworkElement> _entranceQueue = new Queue<FrameworkElement>();
        private DispatcherTimer? _staggerTimer;

        private int _cardsLoadedCount = 0;
        private bool _isEntranceAnimationActive = true;

        public bool IsStaggeredEntranceEnabled { get; set; } = false;

        private bool _isHoveringItem = false;

        public PackagesPage()
        {
            InitializeComponent();

            this.Unloaded += (s, e) =>
            {
                _timer?.Stop();
            };

            Loaded += delegate
            {
                if (HcPanel != null)
                {
                    HcPanel.AnimationFinished = () =>
                    {
                        ReleaseTypewriter();
                    };
                }

                SyncVisualStates();

                _timer = new TimerControlManager(TimeSpan.FromSeconds(5), TimerControlManager.TimerMode.CountUp, time =>
                {
                    if (!this.IsLoaded || this.DispatcherQueue == null)
                    {
                        _timer?.Stop();
                        return;
                    }

                    Task.Run(() =>
                    {
                        _uninstalling.GetInstalledPackages();

                        this.DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.Low, () =>
                        {
                            if (!this.IsLoaded || HcPanel == null) return;

                            UninstallingPakages.OnPackagesChanged();

                            SyncVisualStates();
                        });
                    });
                });

                _timer.Start();
            };
        }

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

                if (sender is ToggleButton toggleButton)
                {
                    string packageName = toggleButton.Name;
                    bool currentlyChecked = toggleButton.IsChecked.GetValueOrDefault();

                    if (!currentlyChecked && packageName == "OneDrive")
                    {
                        await HandleOneDriveRestore(packageName);
                    }
                    else if (currentlyChecked)
                    {
                        if (packageName == "Edge")
                        {
                            var app = Application.Current as App;
                            var mainWindow = app?.MainWindow as MainWindow;

                            if (mainWindow != null)
                            {
                                mainWindow.TxtGlobalTitle.Text = ResourceString.GetString("title_over_pkg") ?? "Uninstallation confirmation";
                                mainWindow.TxtGlobalMessage.Text = ResourceString.GetString("text_over_pkg") ?? "Edge Removal Warning...";
                                mainWindow.TxtGlobalQuestion.Text = ResourceString.GetString("question_over_pkg") ?? "Continue deleting Edge + WebView?";

                                OverlayDialogManager dialogManager = new OverlayDialogManager(
                                    mainWindow.GlobalOverlay,
                                    mainWindow.BtnGlobalDelete,
                                    mainWindow.BtnGlobalCancel);

                                _isWebViewRemoval = await dialogManager.Show();

                                if (_isWebViewRemoval) await HandlePackageRemoval(packageName);
                            }
                        }
                        else
                        {
                            await HandlePackageRemoval(packageName);
                        }
                    }

                    SyncVisualStates();
                }
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

                var app = Application.Current as App;
                var mainWindow = app?.MainWindow as MainWindow;

                if (mainWindow == null) return;

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

        private async Task ExecuteBulkRemoval(PackagesViewModel vm, bool edgeInSelection)
        {
            vm.IsMultiSelectMode = false;
            var packagesToRemove = new List<PackagesModel>(vm.SelectedPackages);
            vm.SelectedPackages.Clear();
            vm.OnPropertyChanged(nameof(vm.RemoveButtonVisibility));

            SyncVisualStates();

            foreach (var package in packagesToRemove)
            {
                string packageName = package.Name;
                bool removeWebView = edgeInSelection && packageName == "Edge";

                await _backgroundQueue.QueueTask(async () =>
                {
                    this.DispatcherQueue.TryEnqueue(() => { UninstallingPakages.HandleAvailabilityStatus(packageName, true); });
                    await UninstallingPakages.RemoveAppxPackage(packageName, removeWebView);
                    await Task.Delay(2000);
                    this.DispatcherQueue.TryEnqueue(() => { UninstallingPakages.HandleAvailabilityStatus(packageName, false); });

                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (ExplorerManager.PackageMapping.TryGetValue(packageName, out bool needRestart) && needRestart)
                        {
                            ExplorerManager.Restart(new Process());
                        }
                        UninstallingPakages.OnPackagesChanged();
                    });
                });
            }

            string notificationMsg = ResourceString.GetString("notif_bulk_completed") ?? "Bulk uninstall Completed";
            NotificationManager.Show("info", notificationMsg).Perform();
        }
        #endregion

        #region Animations & Staggering

        private void PackageView_Loaded(object sender, RoutedEventArgs e)
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
            var targetPackage = vm?[packageName];

            await _backgroundQueue.QueueTask(async () =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    UninstallingPakages.HandleAvailabilityStatus(packageName, true);
                });

                await UninstallingPakages.RemoveAppxPackage(packageName, _isWebViewRemoval);

                await Task.Delay(3000);

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    UninstallingPakages.HandleAvailabilityStatus(packageName, false);

                    if (targetPackage != null)
                    {
                        targetPackage.IsSelected = false;
                        targetPackage.Installed = false;
                    }

                    if (ExplorerManager.PackageMapping.TryGetValue(packageName, out bool needRestart) && needRestart)
                    {
                        ExplorerManager.Restart(new Process());
                    }

                    UninstallingPakages.OnPackagesChanged();
                    SyncVisualStates();
                });
            });
        }

        private async Task HandleOneDriveRestore(string packageName)
        {
            await _backgroundQueue.QueueTask(async () =>
            {
                this.DispatcherQueue.TryEnqueue(() => { UninstallingPakages.HandleAvailabilityStatus(packageName, true); });
                await Task.Delay(3000);
                this.DispatcherQueue.TryEnqueue(() => { UninstallingPakages.HandleAvailabilityStatus(packageName, false); });
            });
        }

        private void HandleAnimationChanged(bool isEnabled)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (HcPanel != null) HcPanel.IsAnimationEnabled = isEnabled;
            });
        }
#endregion
    }
}