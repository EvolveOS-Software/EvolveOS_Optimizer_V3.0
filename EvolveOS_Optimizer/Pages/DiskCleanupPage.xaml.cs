// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;
using CommunityToolkit.Mvvm.Input;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Xaml.Input;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class DiskCleanupPage : Page, ISearchablePage, IPageActions, IPurgeable
    {
        private DiskCleanupViewModel? _viewModel = new();
        public DiskCleanupViewModel ViewModel => _viewModel ?? new();

        private HashSet<Button> _buttonsWithOpenFlyouts = new HashSet<Button>();

        private CancellationTokenSource? _cts;

        #region Page Lifecycle
        public DiskCleanupPage()
        {
            InitializeComponent();

            if (SettingsEngine.IsHighPerformanceModeEnabled)
            {
                this.NavigationCacheMode = NavigationCacheMode.Required;
            }
            else
            {
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }

            this.Loaded += DiskCleanupPage_Loaded;
            this.Unloaded += DiskCleanupPage_Unloaded;
        }

        private async void DiskCleanupPage_Loaded(object sender, RoutedEventArgs e)
        {
            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            if (_cts == null || _cts.IsCancellationRequested)
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
            }

            if (ViewModel.Categories == null || ViewModel.Categories.Count == 0)
            {
                var paths = new List<string> { PathLocator.Files.Winapp2Ini };

                try
                {
                    await ViewModel.LoadWinapp2Async(paths);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DiskCleanup] Load aborted or failed: {ex.Message}");
                }
            }
        }

        private void DiskCleanupPage_Unloaded(object sender, RoutedEventArgs e)
        {
            EfficiencyModeHelper.IsUIWakeLockActive = false;

            if (LocalMachineSettingsEngine.RunOnPriority == Enums.Priority.Low)
            {
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
            }

            _ = Purge();
        }
        #endregion

        #region ISearchablePage & IPageActions
        public void OnSearch(string text) => ViewModel.SearchText = text;

        public void BuildActions(MenuFlyout flyout)
        {
            void Add(string label, Action action)
            {
                var item = new MenuFlyoutItem { Text = label };
                item.Click += (_, _) => action();
                flyout.Items.Add(item);
            }

            Add("Select all", () => ViewModel.SelectAllCommand.Execute(null));
            Add("Select none", () => ViewModel.SelectNoneCommand.Execute(null));
            Add("Select defaults", () => ViewModel.SelectDefaultsCommand.Execute(null));
            flyout.Items.Add(new MenuFlyoutSeparator());
            Add("Expand all", () => ViewModel.ExpandAllCommand.Execute(null));
            Add("Collapse all", () => ViewModel.CollapseAllCommand.Execute(null));
            flyout.Items.Add(new MenuFlyoutSeparator());
            Add("Sort by size ↓", () => ViewModel.SortResultsDescCommand.Execute(null));
            Add("Sort by size ↑", () => ViewModel.SortResultsAscCommand.Execute(null));
            flyout.Items.Add(new MenuFlyoutSeparator());
            Add("Refresh", () => ViewModel.RefreshCommand.Execute(null));
        }
        #endregion

        #region UI Event Handlers

        private async void OpenStorageAnalyzer_Click(object sender, RoutedEventArgs e)
        {
            var drives = ViewModel.GetAvailableDrives();
            DriveOption selectedDrive = drives[0];

            var driveComboBox = new ComboBox
            {
                ItemsSource = drives,
                DisplayMemberPath = "DisplayName",
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 15, 0, 0)
            };

            driveComboBox.SelectionChanged += (s, args) =>
            {
                if (driveComboBox.SelectedItem is DriveOption opt)
                {
                    selectedDrive = opt;
                }
            };

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("analyzer_dialog_title") ?? "Storage Analyzer",
                Content = new StackPanel
                {
                    Children =
            {
                new TextBlock
                {
                    Text = ResourceString.GetString("analyzer_dialog_desc") ?? "Select a local drive to map its storage footprint and find large forgotten files.",
                    TextWrapping = TextWrapping.Wrap
                },
                driveComboBox
            }
                },
                PrimaryButtonText = ResourceString.GetString("analyzer_dialog_analyze") ?? "Analyze",

                SecondaryButtonText = ResourceString.GetString("analyzer_dialog_browse_folder") ?? "Browse Folder...",

                CloseButtonText = ResourceString.GetString("cleanup_btn_close") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.RunStorageAnalyzerCommand.ExecuteAsync(selectedDrive.Path);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                await ViewModel.BrowseAndAnalyzeFolderCommand.ExecuteAsync(null);
            }
        }

        private void AnalyzerShowInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: StorageNode node })
            {
                if (ViewModel.ShowInExplorerCommand.CanExecute(node))
                {
                    ViewModel.ShowInExplorerCommand.Execute(node);
                }
            }
        }

        private async void AnalyzerUnlock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: StorageNode node })
            {
                await ViewModel.UnlockStorageItemCommand.ExecuteAsync(node);
            }
        }

        private async void AnalyzerDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: StorageNode node })
            {
                var dialog = new ContentDialog
                {
                    Title = ResourceString.GetString("analyzer_confirm_delete_title") ?? "Permanently delete item?",
                    Content = string.Format(ResourceString.GetString("analyzer_confirm_delete_desc") ?? "Are you sure you want to permanently delete '{0}'? This action cannot be undone.", node.Name),
                    PrimaryButtonText = ResourceString.GetString("analyzer_btn_delete") ?? "Delete",
                    CloseButtonText = ResourceString.GetString("cleanup_btn_close") ?? "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    await ViewModel.DeleteStorageItemCommand.ExecuteAsync(node);
                }
            }
        }

        private void CloseStorageAnalyzer_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsAnalyzerViewActive = false;
        }

        private void ResultsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ScanResultLine line && line.Result is not null)
                ViewModel.SelectedResultLine = line;
        }

        private void DetailList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not DetailLine { IsHeader: false } line) return;
            var path = line.Text;
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith("HK", StringComparison.OrdinalIgnoreCase)) return;

            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }

        private async void EntryAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: DiskCleanupEntryViewModel vm })
                await ViewModel.AnalyzeSingleEntryAsync(vm);
        }

        private async void EntryClean_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: DiskCleanupEntryViewModel vm })
            {
                if (!await ConfirmWarningsAsync(ViewModel.GetWarningsForEntry(vm)))
                    return;

                await ViewModel.CleanSingleEntryAsync(vm);
            }
        }

        private async void CatAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: DiskCleanupCategoryViewModel vm })
                await ViewModel.AnalyzeCategoryAsync(vm);
        }

        private async void CatClean_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: DiskCleanupCategoryViewModel vm })
            {
                if (!await ConfirmWarningsAsync(ViewModel.GetWarningsForCategory(vm)))
                    return;

                await ViewModel.CleanCategoryAsync(vm);
            }
        }

        private async void PerformCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.RunCleanerCommand.CanExecute(null))
                return;

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("cleanup_confirm_title") ?? "Ready to clean?",
                Content = ResourceString.GetString("cleanup_confirm_content") ?? "This will permanently delete the selected files. Are you sure you want to continue?",
                PrimaryButtonText = ResourceString.GetString("cleanup_confirm_primary") ?? "Clean",
                CloseButtonText = ResourceString.GetString("cleanup_confirm_cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.ContentArea.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result != ContentDialogResult.Primary)
                return;

            if (!await ConfirmWarningsAsync(ViewModel.GetWarningsForSelectedEntries()))
                return;

            await ((IAsyncRelayCommand)ViewModel.RunCleanerCommand).ExecuteAsync(null);
        }

        private void AnalyzerBarSegment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is StorageInsight insight)
            {
                if (insight.TargetNode != null)
                {
                    if (ViewModel.AnalyzedNodes.Count > 0)
                    {
                        ViewModel.AnalyzedNodes[0].IsExpanded = true;
                    }

                    insight.TargetNode.IsExpanded = true;

                    StorageTreeView.SelectedItem = insight.TargetNode;

                    ViewModel.GenerateAnalyzerInsights(insight.TargetNode);

                    StorageTreeView.DispatcherQueue.TryEnqueue(() =>
                    {
                        var container = StorageTreeView.ContainerFromItem(insight.TargetNode) as FrameworkElement;
                        container?.StartBringIntoView(new BringIntoViewOptions
                        {
                            VerticalAlignmentRatio = 0.5
                        });
                    });
                }
            }
        }

        private async void CategoryExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            await Task.Delay(100);

            sender.StartBringIntoView(new BringIntoViewOptions
            {
                VerticalAlignmentRatio = 0.0f,
                HorizontalAlignmentRatio = 0.0f
            });
        }

        private void StorageTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is StorageNode selectedNode)
            {
                ViewModel.GenerateAnalyzerInsights(selectedNode);
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var window = App.MainWindow;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add(".ini");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                ViewModel.CustomPath = file.Path;
            }
        }

        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menu && menu.DataContext is DetailLine line)
            {
                if (!string.IsNullOrEmpty(line.Path))
                {
                    try
                    {
                        Process.Start("explorer.exe", $"/select,\"{line.Path}\"");
                    }
                    catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                }
            }
        }

        private async void InspectConflict_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menu && menu.DataContext is DetailLine line)
            {
                if (string.IsNullOrEmpty(line.Path)) return;

                var blockers = UnlockHandleHelper.GetLockingProcessNames(line.Path);
                string message;

                if (blockers.Count > 0)
                {
                    var header = ResourceString.GetString("cleanup_conflict_message_header");
                    message = $"{header}{Environment.NewLine}{Environment.NewLine}• {string.Join($"{Environment.NewLine}• ", blockers)}";
                }
                else
                {
                    message = ResourceString.GetString("cleanup_conflict_message_none");
                }

                ContentDialog dialog = new ContentDialog
                {
                    Title = ResourceString.GetString("cleanup_conflict_title"),
                    Content = message,
                    CloseButtonText = ResourceString.GetString("cleanup_btn_close"),
                    XamlRoot = this.XamlRoot,
                    DefaultButton = ContentDialogButton.Close
                };

                dialog.FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Jura"];

                await dialog.ShowAsync();
            }
        }

        private async Task<bool> ConfirmWarningsAsync(IReadOnlyList<string> warnings)
        {
            if (warnings.Count == 0)
                return true;

            var title = ResourceHelper.GetResourceString("diag_cleaning_warning_title");
            var btnContinue = ResourceHelper.GetResourceString("diag_cleaning_warning_continue");
            var btnCancel = ResourceHelper.GetResourceString("diag_cleaning_warning_cancel");
            var bodyText = ResourceHelper.GetResourceString("diag_cleaning_warning_body");

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title.StartsWith("[") ? "Cleaning warning" : title,
                PrimaryButtonText = btnContinue.StartsWith("[") ? "Continue" : btnContinue,
                CloseButtonText = btnCancel.StartsWith("[") ? "Cancel" : btnCancel,
                DefaultButton = ContentDialogButton.Close,
                Content = new ScrollViewer
                {
                    MaxHeight = 360,
                    Content = new TextBlock
                    {
                        Text = (bodyText.StartsWith("[") ? "The selected Winapp2 entries include the following warnings:" : bodyText) +
                               $"{Environment.NewLine}{Environment.NewLine}" +
                               string.Join($"{Environment.NewLine}{Environment.NewLine}", warnings),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                ViewModel.SearchText = sender.Text;
            }
        }

        private void TogglePaneButton_Click(object sender, RoutedEventArgs e)
        {
            MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
        }
        #endregion

        #region Visual States (Hover Effects)

        private void CatHeader_PointerEntered(object sender, PointerRoutedEventArgs e) =>
            SetMenuButtonOpacity(sender, 1);

        private void CatHeader_PointerExited(object sender, PointerRoutedEventArgs e) =>
            SetMenuButtonOpacityIfFlyoutClosed(sender, 0);

        private void EntryRow_PointerEntered(object sender, PointerRoutedEventArgs e) =>
            SetMenuButtonOpacity(sender, 1);

        private void EntryRow_PointerExited(object sender, PointerRoutedEventArgs e) =>
            SetMenuButtonOpacityIfFlyoutClosed(sender, 0);

        private void SetMenuButtonOpacity(object sender, double opacity)
        {
            if (sender is Grid g)
                foreach (var btn in g.Children.OfType<Button>())
                    btn.Opacity = opacity;
        }

        private void SetMenuButtonOpacityIfFlyoutClosed(object sender, double opacity)
        {
            if (sender is Grid g)
            {
                foreach (var btn in g.Children.OfType<Button>())
                {
                    if (!_buttonsWithOpenFlyouts.Contains(btn))
                    {
                        btn.Opacity = opacity;
                    }
                }
            }
        }

        private void ActionMenu_Opened(object sender, object e)
        {
            if (sender is MenuFlyout flyout && flyout.Target is Button btn)
            {
                _buttonsWithOpenFlyouts.Add(btn);
                btn.Opacity = 1;
            }
        }

        private void ActionMenu_Closed(object sender, object e)
        {
            if (sender is MenuFlyout flyout && flyout.Target is Button btn)
            {
                _buttonsWithOpenFlyouts.Remove(btn);
                btn.Opacity = 0;
            }
        }

        #endregion

        #region Purge Page
        public async Task Purge()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

            if (_cts != null)
            {
                try { _cts.Cancel(); _cts.Dispose(); } catch { }
                _cts = null;
            }

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and ViewModel...");

                if (_viewModel != null)
                {
                    _viewModel.Categories?.Clear();
                    _viewModel.ResultLines?.Clear();
                    _viewModel.DetailLines?.Clear();
                    _viewModel.HistoryChart?.Clear();
                    _viewModel.CategoryInsights?.Clear();
                    _viewModel.AnalyzedNodes?.Clear();

                    _viewModel = null;
                }

                _buttonsWithOpenFlyouts.Clear();

                this.Loaded -= DiskCleanupPage_Loaded;
                this.Unloaded -= DiskCleanupPage_Unloaded;

                this.DataContext = null;
                this.Content = null;
                this.Bindings?.StopTracking();

                _ = Task.Run(() =>
                {
                    DiagnosticsPageViewModel.Current.ForceImmediateMemoryCleanup();
                });
            }
            else
            {
                Debug.WriteLine($"[{this.GetType().Name}] High Performance Mode: State preserved in RAM cache.");
            }
        }

        #endregion
    }
}