// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Threading;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;

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

        #endregion

        public StartupManagerPage()
        {
            this.InitializeComponent();
            this.Unloaded += Page_Unloaded;

            StartupAppsListView.ItemsSource = _startupApps;
            _ = LoadDataAsync();
        }

        #endregion

        #region Lifecycle & Data Loading

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isUnloading = true;
            Purge();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                LoadingRing.Visibility = Visibility.Visible;
                StartupAppsListView.Visibility = Visibility.Collapsed;

                _allApps = await StartupManagerHelper.GetStartupAppsAsync();

                if (_isUnloading) return;

                var delayedApps = await StartupManagerHelper.GetDelayedAppsStateAsync();
                foreach (var app in _allApps)
                {
                    if (delayedApps.TryGetValue(app.Name, out int savedSeconds))
                    {
                        app.DelaySeconds = savedSeconds;
                    }
                }

                ApplyFiltersAndSearch();

                string activeProfile = await StartupManagerHelper.DetermineActiveProfileAsync(_allApps);

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
        }

        private void UpdateSummary()
        {
            TotalAppsText.Text = _allApps.Count.ToString();
            EnabledAppsText.Text = _allApps.Count(a => a.IsEnabled).ToString();
            DisabledAppsText.Text = _allApps.Count(a => !a.IsEnabled).ToString();
            ResultsText.Text = string.Format(ResourceString.GetString("startup_manager_page_results_text"), _startupApps.Count, _allApps.Count);
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
                    Title = ResourceString.GetString("startup_manager_page_delete_title"),
                    Content = string.Format(ResourceString.GetString("startup_manager_page_delete_content"), app.DisplayName),
                    PrimaryButtonText = ResourceString.GetString("startup_manager_page_delete_confirm"),
                    CloseButtonText = ResourceString.GetString("startup_manager_page_delete_cancel"),
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
            DotProfileGaming.Visibility = Visibility.Collapsed;
            DotProfileGaming.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LimeGreen);

            DotProfileWork.Visibility = Visibility.Collapsed;
            DotProfileWork.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LimeGreen);

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
                    DotProfileGaming.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Orange);
                    ToolTipService.SetToolTip(BtnProfileGaming, ResourceString.GetString("startup_manager_page_profile_gaming_modified"));
                }
                else if (_lastActiveProfile == "Work")
                {
                    DotProfileWork.Visibility = Visibility.Visible;
                    DotProfileWork.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Orange);
                    ToolTipService.SetToolTip(BtnProfileWork, ResourceString.GetString("startup_manager_page_profile_work_modified"));
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
                Title = ResourceString.GetString("startup_manager_page_profile_saved_title"),
                Content = string.Format(ResourceString.GetString("startup_manager_page_profile_saved_content_dynamic"), localizedName),
                CloseButtonText = ResourceString.GetString("startup_manager_page_profile_saved_ok"),
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

        public void Purge()
        {
            Debug.WriteLine("[StartupManagerPage] Purge initiated...");

            _isUnloading = true;
            Unloaded -= Page_Unloaded;

            if (_delayDebounceTokens != null)
            {
                foreach (var cts in _delayDebounceTokens.Values)
                {
                    try
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    catch { }
                }
                _delayDebounceTokens.Clear();
            }

            _startupApps?.Clear();
            _allApps?.Clear();

            StartupAppsListView.ItemsSource = null;
            this.DataContext = null;
            this.Content = null;

            Debug.WriteLine("[StartupManagerPage] Purge Complete.");
        }

        #endregion
    }
}