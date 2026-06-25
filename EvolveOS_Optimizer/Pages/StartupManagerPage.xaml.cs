// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Threading;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class StartupManagerPage : Page, IPurgeable
    {
        #region Fields & Constructor

        #region Private Fields

        private ObservableCollection<StartupApp> _startupApps = new();
        private List<StartupApp> _allApps = new();

        private static string _lastActiveProfile = "";
        private readonly Dictionary<string, CancellationTokenSource> _delayDebounceTokens = new();

        private bool _isInitialLoad = true;
        private bool _isUnloading = false;
        private bool _isUpdatingUI = false;

        private CancellationTokenSource? _cts;
        #endregion

        public StartupManagerPage()
        {
            this.InitializeComponent();

            if (SettingsEngine.IsHighPerformanceModeEnabled)
            {
                this.NavigationCacheMode = NavigationCacheMode.Required;
            }
            else
            {
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }

            this.Loaded += Page_Loaded;
            this.Unloaded += Page_Unloaded;

            StartupAppsListView.ItemsSource = _startupApps;
        }

        #endregion

        #region Lifecycle & Data Loading

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _isUnloading = false;

            if (_cts != null)
            {
                try { _cts.Cancel(); _cts.Dispose(); } catch { }
            }
            _cts = new CancellationTokenSource();

            if (_allApps.Count == 0)
            {
                await LoadDataAsync();
            }

            AiExplainerService.PreWarmConnection();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isUnloading = true;
            _ = Purge();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var token = _cts?.Token ?? default;

                LoadingRing.Visibility = Visibility.Visible;
                StartupAppsListView.Visibility = Visibility.Collapsed;

                _allApps = await StartupManagerHelper.GetStartupAppsAsync();

                if (_isUnloading || token.IsCancellationRequested) return;

                var delayedApps = await StartupManagerHelper.GetDelayedAppsStateAsync();

                if (token.IsCancellationRequested) return;

                foreach (var app in _allApps)
                {
                    if (delayedApps.TryGetValue(app.Name, out int savedSeconds))
                    {
                        app.DelaySeconds = savedSeconds;
                    }
                }

                ApplyFiltersAndSearch();

                string activeProfile = await StartupManagerHelper.DetermineActiveProfileAsync(_allApps);

                if (token.IsCancellationRequested) return;

                if (activeProfile != "Modified" && activeProfile != "Custom")
                {
                    _lastActiveProfile = activeProfile;
                }

                SetActiveProfileIndicator(activeProfile);

                LoadingRing.Visibility = Visibility.Collapsed;
                StartupAppsListView.Visibility = Visibility.Visible;
                _isInitialLoad = false;
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                ErrorLogging.LogDebug($"[StartupManager] COMException (Page likely unloaded during fetch). Safe to ignore: {comEx.Message}");
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            finally
            {
                if (LoadingRing != null) LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateSummary()
        {
            TotalAppsText.Text = _allApps.Count.ToString();
            EnabledAppsText.Text = _allApps.Count(a => a.IsEnabled).ToString();
            DisabledAppsText.Text = _allApps.Count(a => !a.IsEnabled).ToString();
            ResultsText.Text = string.Format(ResourceString.GetString("startup_manager_page_results_text") ?? "Showing {0} of {1} apps", _startupApps.Count, _allApps.Count);
        }

        #endregion

        #region Filtering & Sorting

        private void ApplyFiltersAndSearch()
        {
            if (_isUnloading) return;

            var query = SearchBox.Text.ToLowerInvariant();
            var filterIndex = FilterComboBox.SelectedIndex;

            var filtered = _allApps.Where(app =>
            {
                bool matchesSearch = string.IsNullOrEmpty(query) ||
                                     app.DisplayName.ToLowerInvariant().Contains(query) ||
                                     app.Path.ToLowerInvariant().Contains(query);

                bool matchesStatus = filterIndex switch
                {
                    1 => app.IsEnabled,
                    2 => !app.IsEnabled,
                    _ => true
                };

                return matchesSearch && matchesStatus;
            }).OrderBy(a => a.DisplayName).ToList();

            _startupApps.Clear();
            foreach (var app in filtered)
            {
                _startupApps.Add(app);
            }

            UpdateSummary();
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                ApplyFiltersAndSearch();
            }
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialLoad) ApplyFiltersAndSearch();
        }

        private void SortHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string sortBy)
            {
                var sorted = sortBy switch
                {
                    "Name" => _startupApps.OrderBy(a => a.DisplayName).ToList(),
                    "Status" => _startupApps.OrderByDescending(a => a.IsEnabled).ToList(),
                    "Source" => _startupApps.OrderBy(a => a.SourceLocation).ToList(),
                    _ => _startupApps.ToList()
                };

                _startupApps.Clear();
                foreach (var app in sorted) _startupApps.Add(app);
            }
        }

        #endregion

        #region AI Explainer
        public bool IsAiEnabled()
        {
            var activeProvider = LocalMachineSettingsEngine.ActiveAiProvider;
            return activeProvider switch
            {
                AiProvider.Groq => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.GroqApiKey),
                AiProvider.Gemini => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.GeminiApiKey),
                AiProvider.OpenRouter => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.OpenRouterApiKey),
                AiProvider.Cohere => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.CohereApiKey),
                AiProvider.Mistral => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.MistralApiKey),
                _ => false
            };
        }

        private async void ExplainStartupApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is StartupApp app)
            {
                var flyout = button.Flyout as Flyout;
                if (flyout == null) return;

                var stackPanel = flyout.Content as StackPanel;
                var textBlock = stackPanel?.Children.OfType<TextBlock>().FirstOrDefault(x => x.Name == "AiExplanationText");

                if (textBlock == null) return;

                textBlock.Text = ResourceString.GetString("ai_explainer_thinking") ?? "Thinking...";

                string context = $"Name: {app.DisplayName}\n" +
                                 $"Publisher: {app.Publisher}\n" +
                                 $"Path: {app.Path}\n" +
                                 $"Source/Registry: {app.SourceLocation}";

                string category = ResourceString.GetString("startup_manager_page_category_name") ?? "Startup Application";

                string explanation = await AiExplainerService.ExplainGenericItemAsync(
                    itemName: app.DisplayName,
                    itemCategory: category,
                    contextDetails: context
                );

                textBlock.Text = explanation;
            }
        }
        #endregion

        #region Standard Actions

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isUnloading || _isInitialLoad || _isUpdatingUI) return;

            if (sender is ToggleSwitch toggle && toggle.Tag is string id)
            {
                var app = _allApps.FirstOrDefault(a => a.Id == id);

                if (app != null && app.IsEnabled != toggle.IsOn)
                {
                    bool success = await StartupManagerHelper.ToggleStartupAppAsync(app, toggle.IsOn);

                    if (success)
                    {
                        app.IsEnabled = toggle.IsOn;
                        UpdateSummary();

                        SetActiveProfileIndicator("Modified");
                    }
                    else
                    {
                        _isUpdatingUI = true;
                        toggle.IsOn = app.IsEnabled;
                        _isUpdatingUI = false;
                    }
                }
            }
        }

        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                try
                {
                    var cleanPath = path.Split(" -")[0].Split(" /")[0].Replace("\"", "");

                    if (System.IO.File.Exists(cleanPath) || System.IO.Directory.Exists(cleanPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{cleanPath}\"");
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug($"Failed to open location for startup app: {ex.Message}");
                }
            }
        }

        private async void DeleteApp_Click(object sender, RoutedEventArgs e)
        {
            if (_isUnloading) return;

            if (sender is Button btn && btn.Tag is string id)
            {
                var app = _allApps.FirstOrDefault(a => a.Id == id);
                if (app == null) return;

                ContentDialog dialog = new ContentDialog
                {
                    Title = ResourceString.GetString("startup_manager_page_delete_title") ?? "Delete Startup Entry",
                    Content = string.Format(ResourceString.GetString("startup_manager_page_delete_content") ?? "Are you sure you want to permanently delete {0} from startup?", app.DisplayName),
                    PrimaryButtonText = ResourceString.GetString("startup_manager_page_delete_confirm") ?? "Delete",
                    CloseButtonText = ResourceString.GetString("startup_manager_page_delete_cancel") ?? "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    bool success = await StartupManagerHelper.DeleteStartupAppAsync(app);

                    if (success)
                    {
                        _allApps.Remove(app);
                        _startupApps.Remove(app);
                        UpdateSummary();
                    }
                }
            }
        }

        #endregion

        #region Premium Features (Smart Profiles & Delayed Startup)

        private void SetActiveProfileIndicator(string profileKey)
        {
            if (DotProfileGaming == null || DotProfileWork == null) return;

            DotProfileGaming.Visibility = Visibility.Collapsed;
            DotProfileGaming.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen);

            DotProfileWork.Visibility = Visibility.Collapsed;
            DotProfileWork.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen);

            if (profileKey == "Gaming" || profileKey == "Work")
            {
                _lastActiveProfile = profileKey;
            }

            if (profileKey == "Gaming")
            {
                DotProfileGaming.Visibility = Visibility.Visible;
            }
            else if (profileKey == "Work")
            {
                DotProfileWork.Visibility = Visibility.Visible;
            }
            else if (profileKey == "Modified")
            {
                if (_lastActiveProfile == "Gaming")
                {
                    DotProfileGaming.Visibility = Visibility.Visible;
                    DotProfileGaming.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                    ToolTipService.SetToolTip(BtnProfileGaming, ResourceString.GetString("startup_manager_page_profile_gaming_modified") ?? "Gaming Profile (Modified)");
                }
                else if (_lastActiveProfile == "Work")
                {
                    DotProfileWork.Visibility = Visibility.Visible;
                    DotProfileWork.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                    ToolTipService.SetToolTip(BtnProfileWork, ResourceString.GetString("startup_manager_page_profile_work_modified") ?? "Work Profile (Modified)");
                }
            }
        }

        private async void ApplyProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_isUnloading) return;

            if (sender is Button btn && btn.Tag is string profileName)
            {
                LoadingRing.Visibility = Visibility.Visible;

                _isUpdatingUI = true;
                await StartupManagerHelper.ApplyProfileAsync(profileName, _allApps);
                _isUpdatingUI = false;

                ApplyFiltersAndSearch();
                LoadingRing.Visibility = Visibility.Collapsed;

                SetActiveProfileIndicator(profileName);
            }
        }

        private async void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_isUnloading) return;

            string targetProfile = string.IsNullOrEmpty(_lastActiveProfile) ? "Custom" : _lastActiveProfile;

            await StartupManagerHelper.SaveProfileAsync(targetProfile, _allApps);

            SetActiveProfileIndicator(targetProfile);

            string localizedName = ResourceString.GetString($"startup_manager_page_profile_{targetProfile.ToLower()}");
            if (string.IsNullOrEmpty(localizedName)) localizedName = targetProfile;

            ContentDialog dialog = new ContentDialog
            {
                Title = ResourceString.GetString("startup_manager_page_profile_saved_title") ?? "Profile Saved",
                Content = string.Format(ResourceString.GetString("startup_manager_page_profile_saved_content_dynamic") ?? "Your current startup configuration has been saved to the {0} profile.", localizedName),
                CloseButtonText = ResourceString.GetString("startup_manager_page_profile_saved_ok") ?? "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async void DelayApp_Click(object sender, RoutedEventArgs e)
        {
            if (_isUnloading) return;

            if (sender is Button btn && btn.Tag is string id)
            {
                var app = _allApps.FirstOrDefault(a => a.Id == id);
                if (app != null)
                {
                    int nextDelay = app.DelaySeconds switch
                    {
                        0 => 15,
                        15 => 30,
                        30 => 45,
                        45 => 60,
                        60 => 0,
                        _ => 0
                    };

                    _isUpdatingUI = true;
                    app.DelaySeconds = nextDelay;
                    app.IsEnabled = (nextDelay == 0);
                    _isUpdatingUI = false;

                    UpdateSummary();
                    SetActiveProfileIndicator("Modified");

                    if (_delayDebounceTokens.TryGetValue(app.Id, out var existingToken))
                    {
                        existingToken.Cancel();
                        existingToken.Dispose();
                    }

                    var cts = new System.Threading.CancellationTokenSource();
                    _delayDebounceTokens[app.Id] = cts;

                    try
                    {
                        await Task.Delay(700, cts.Token);

                        if (nextDelay > 0)
                        {
                            bool success = await StartupManagerHelper.DelayStartupAppAsync(app, nextDelay);
                            if (success)
                            {
                                await StartupManagerHelper.SaveDelayedAppStateAsync(app.Name, nextDelay);
                            }
                            else
                            {
                                _isUpdatingUI = true;
                                app.IsEnabled = true;
                                app.DelaySeconds = 0;
                                _isUpdatingUI = false;
                            }
                        }
                        else
                        {
                            bool success = await StartupManagerHelper.RemoveDelayStartupAppAsync(app);
                            if (success)
                            {
                                await StartupManagerHelper.SaveDelayedAppStateAsync(app.Name, 0);
                            }
                            else
                            {
                                _isUpdatingUI = true;
                                app.IsEnabled = false;
                                _isUpdatingUI = false;
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // User clicked again, this task is discarded
                    }
                    catch (Exception ex)
                    {
                        ErrorLogging.LogDebug(ex);
                    }
                }
            }
        }

        #endregion

        #region Purge Page

        public async Task Purge()
        {
            Debug.WriteLine("[StartupManagerPage] Caching Purge requested. Pausing page...");

            _isUnloading = true;

            if (_cts != null)
            {
                try { _cts.Cancel(); _cts.Dispose(); } catch { }
                _cts = null;
            }

            if (_delayDebounceTokens != null)
            {
                foreach (var cts in _delayDebounceTokens.Values)
                {
                    try { cts.Cancel(); cts.Dispose(); } catch { }
                }
                _delayDebounceTokens.Clear();
            }

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and App Collections...");

                _allApps.Clear();
                _startupApps.Clear();

                this.Loaded -= Page_Loaded;
                this.Unloaded -= Page_Unloaded;

                if (StartupAppsListView != null) StartupAppsListView.ItemsSource = null;

                this.DataContext = null;
                this.Content = null;
                //this.Bindings?.StopTracking();

                _ = Task.Run(() =>
                {
                    DiagnosticsPageViewModel.Current?.ForceImmediateMemoryCleanup();
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