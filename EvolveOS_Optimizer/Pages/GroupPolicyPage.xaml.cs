// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Text;
using System.Threading;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Core.ViewModel.Items;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class GroupPolicyPage : Page, IPurgeable
{
    private CancellationTokenSource? _cancellationTokenSource;
    private IReadOnlyList<GroupPolicyHelper.PolicyState>? _policyStates;
    private string? _pendingScrollTarget;

    private string _searchQuery = "";

    public List<PolicyStateViewModel> DisplayedPolicies => _policyStates?
        .Where(s => string.IsNullOrWhiteSpace(_searchQuery) ||
                    s.Policy.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    s.Policy.Category.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(s => s.IsConfigured)
        .ThenBy(s => s.Policy.Category)
        .ThenBy(s => s.Policy.Name)
        .Select(s => new PolicyStateViewModel(s))
        .ToList() ?? new List<PolicyStateViewModel>();

    public GroupPolicyPage()
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

        ErrorLogging.LogDebug(new Exception(ResourceString.GetString("Initializing GroupPolicyPage")));

        Loaded += GroupPolicyPage_Loaded;
        Unloaded += GroupPolicyPage_Unloaded;
    }

    #region Page Lifecycle & Navigation
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string optionTag && !string.IsNullOrEmpty(optionTag))
        {
            _pendingScrollTarget = optionTag;
        }
    }

    private async void GroupPolicyPage_Loaded(object sender, RoutedEventArgs e)
    {
        ConfiguredPoliciesListView.SelectionChanged -= ConfiguredPoliciesListView_SelectionChanged;
        ConfiguredPoliciesListView.SelectionChanged += ConfiguredPoliciesListView_SelectionChanged;

        if (_policyStates == null)
        {
            await ScanPoliciesAsync();
        }
        else
        {
            UpdateSummary();
            UpdateCategorySummary();
            UpdateDisplayedPoliciesList();
        }

        if (!string.IsNullOrEmpty(_pendingScrollTarget))
        {
            await ScrollToElementHelper.ScrollToElementAsync(this, _pendingScrollTarget);
            _pendingScrollTarget = null;
        }

        AiExplainerService.PreWarmConnection();
    }

    private void GroupPolicyPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _ = Purge();
    }
    #endregion

    private void ConfiguredPoliciesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RemoveSelectedButton.IsEnabled = ConfiguredPoliciesListView.SelectedItems.Count > 0;
    }

    #region Core Scanning Engine
    private async Task ScanPoliciesAsync()
    {
        if (_cancellationTokenSource != null)
        {
            try
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            catch (ObjectDisposedException) { }
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            ScanProgressRing.Visibility = Visibility.Visible;
            ScanProgressRing.IsActive = true;
            SummaryText.Text = ResourceString.GetString("GroupPolicyPage_ScanningPolicies") ?? "Scanning 3000+ ADMX OS Policies...";
            RefreshButton.IsEnabled = false;
            RemoveAllButton.IsEnabled = false;

            var admxMap = await AdmxEngine.LoadAllLocalPoliciesAsync(token);

            var dynamicStates = new List<GroupPolicyHelper.PolicyState>();
            await Task.Run(() =>
            {
                foreach (var pol in admxMap)
                {
                    if (token.IsCancellationRequested) break;
                    dynamicStates.Add(DetectStateLocally(pol));
                }
            }, token);

            _policyStates = dynamicStates;

            DispatcherQueue.TryEnqueue(() =>
            {
                if (this.XamlRoot == null || token.IsCancellationRequested) return;

                UpdateSummary();
                UpdateCategorySummary();
                UpdateDisplayedPoliciesList();
            });
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (this.XamlRoot != null && !token.IsCancellationRequested)
                    SummaryText.Text = ResourceString.GetString("GroupPolicyPage_ScanError") ?? "Error scanning ADMX policies.";
            });
        }
        finally
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (this.XamlRoot != null && !token.IsCancellationRequested)
                {
                    ScanProgressRing.Visibility = Visibility.Collapsed;
                    ScanProgressRing.IsActive = false;
                    RefreshButton.IsEnabled = true;
                }

                EfficiencyModeHelper.IsUIWakeLockActive = false;
                if (LocalMachineSettingsEngine.RunOnPriority == Core.Enums.Priority.Low)
                {
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
                }
            });
        }
    }

    private GroupPolicyHelper.PolicyState DetectStateLocally(GroupPolicyHelper.PolicyEntry policy)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(policy.Hive, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);
            using var subKey = baseKey.OpenSubKey(policy.RegistryPath, writable: false);

            if (subKey == null)
                return new GroupPolicyHelper.PolicyState { Policy = policy, IsConfigured = false };

            var val = subKey.GetValue(policy.ValueName);

            return new GroupPolicyHelper.PolicyState
            {
                Policy = policy,
                IsConfigured = val != null,
                CurrentValue = val,
                ActualValueKind = val != null ? subKey.GetValueKind(policy.ValueName) : null
            };
        }
        catch
        {
            return new GroupPolicyHelper.PolicyState { Policy = policy, IsConfigured = false };
        }
    }
    #endregion

    #region UI Update Helpers
    private void UpdateSummary()
    {
        if (_policyStates == null) return;

        var configuredCount = _policyStates.Count(s => s.IsConfigured);
        var totalCount = _policyStates.Count;

        string format = ResourceString.GetString("GroupPolicyPage_ConfiguredPoliciesCount") ?? "{0} active overrides out of {1} total OS policies.";
        SummaryText.Text = string.Format(format, configuredCount, totalCount);
        RemoveAllButton.IsEnabled = configuredCount > 0;
    }

    private void UpdateCategorySummary()
    {
        if (_policyStates == null) return;

        var categoryGroups = _policyStates
            .GroupBy(s => s.Policy.Category)
            .Select(g => new CategorySummaryItem
            {
                Category = g.Key,
                TotalCount = g.Count(),
                ConfiguredCount = g.Count(s => s.IsConfigured),
                IconGlyph = GetCategoryIcon(g.Key)
            })
            .OrderByDescending(c => c.ConfiguredCount)
            .ThenBy(c => c.Category)
            .ToList();

        CategorySummaryRepeater.ItemsSource = categoryGroups;
    }

    private void UpdateDisplayedPoliciesList()
    {
        if (_policyStates == null) return;

        var policies = DisplayedPolicies;

        if (policies.Count == 0)
        {
            ConfiguredPoliciesListView.Visibility = Visibility.Collapsed;
            NoPoliciesPanel.Visibility = Visibility.Visible;
            ConfiguredPoliciesListView.ItemsSource = null;
        }
        else
        {
            ConfiguredPoliciesListView.Visibility = Visibility.Visible;
            NoPoliciesPanel.Visibility = Visibility.Collapsed;
            ConfiguredPoliciesListView.ItemsSource = policies;
        }
    }

    private static string GetCategoryIcon(string category)
    {
        return category switch
        {
            "WindowsUpdate" => "\uE777",
            "Privacy" => "\uE72E",
            "Search" => "\uE721",
            "WindowsStore" => "\uE719",
            "OneDrive" => "\uE753",
            "WindowsDefenderSecurityCenter" => "\uE72E",
            "WindowsDefender" => "\uE72E",
            "ErrorReporting" => "\uE783",
            "SystemRestore" => "\uE777",
            "WindowsAnytimeUpgrade" => "\uF1AD",
            "AppPrivacy" => "\uE71D",
            "WindowsInkWorkspace" => "\uE929",
            "Biometrics" => "\uE928",
            "LocationProvider" => "\uE81D",
            "FindMyDevice" => "\uE707",
            "Messaging" => "\uE715",
            "OSCV" => "\uE77F",
            "Speech" => "\uE720",
            "GameDVR" => "\uE7FC",
            "NewsAndInterests" => "\uE71B",
            "WindowsAI" => "\uE946",
            "MicrosoftEdge" => "\uE774",
            "FileHistory" => "\uE8F1",
            "StartMenu" => "\uE80F",
            "ControlPanelDisplay" => "\uE713",
            "Desktop" => "\uE7F4",
            "Taskbar" => "\uE80F",
            _ => "\uE713"
        };
    }
    #endregion

    #region Real-Time Search
    private void PolicySearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            _searchQuery = sender.Text;
            UpdateDisplayedPoliciesList();
        }
    }
    #endregion

    #region Backup & Export to .reg
    private async void ExportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_policyStates == null) return;
        var configured = _policyStates.Where(s => s.IsConfigured).ToList();

        if (configured.Count == 0)
        {
            App.ShowNotification(
                ResourceString.GetString("GroupPolicyPage_ExportFailedTitle"),
                ResourceString.GetString("GroupPolicyPage_ExportFailedMsg"),
                InfoBarSeverity.Warning, 3000);
            return;
        }

        try
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Registry File", new List<string>() { ".reg" });
            savePicker.SuggestedFileName = "EvolveOS_Policy_Backup";

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Windows Registry Editor Version 5.00");
                sb.AppendLine();

                var grouped = configured.GroupBy(s => s.Policy.Hive + "\\" + s.Policy.RegistryPath);

                foreach (var group in grouped)
                {
                    string hiveName = group.Key.StartsWith("HKLM") ? "HKEY_LOCAL_MACHINE" : "HKEY_CURRENT_USER";
                    string cleanPath = group.Key.Substring(group.Key.IndexOf('\\') + 1);

                    sb.AppendLine($"[{hiveName}\\{cleanPath}]");

                    var regHive = hiveName == "HKEY_LOCAL_MACHINE" ? Registry.LocalMachine : Registry.CurrentUser;
                    using var key = regHive.OpenSubKey(cleanPath);

                    foreach (var pol in group)
                    {
                        if (key != null)
                        {
                            object? val = key.GetValue(pol.Policy.ValueName);
                            RegistryValueKind kind = key.GetValueKind(pol.Policy.ValueName);

                            if (val != null)
                            {
                                if (kind == RegistryValueKind.DWord)
                                    sb.AppendLine($"\"{pol.Policy.ValueName}\"=dword:{((int)val):x8}");
                                else if (kind == RegistryValueKind.String)
                                    sb.AppendLine($"\"{pol.Policy.ValueName}\"=\"{val}\"");
                            }
                        }
                    }
                    sb.AppendLine();
                }

                await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString());
                App.ShowNotification(
                    ResourceString.GetString("GroupPolicyPage_ExportSuccessTitle"),
                    ResourceString.GetString("GroupPolicyPage_ExportSuccessMsg"),
                    InfoBarSeverity.Success, 4000);
            }
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            App.ShowNotification(
                ResourceString.GetString("GroupPolicyPage_ExportErrorTitle"),
                ResourceString.GetString("GroupPolicyPage_ExportErrorMsg"),
                InfoBarSeverity.Error, 4000);
        }
    }
    #endregion

    #region One-Click Optimizer Presets
    private async void ApplyPrivacyPresetButton_Click(object sender, RoutedEventArgs e)
    {
        var script = @"
        reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection"" /v AllowTelemetry /t REG_DWORD /d 0 /f
        reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search"" /v AllowCortana /t REG_DWORD /d 0 /f
        reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search"" /v DisableWebSearch /t REG_DWORD /d 1 /f
        ";

        string presetName = ResourceString.GetString("GroupPolicyPage_PresetPrivacyTitle") ?? "Ultimate Privacy";
        string successMsg = ResourceString.GetString("GroupPolicyPage_PresetPrivacySuccess") ?? "Ultimate Privacy has been applied successfully.";

        await ApplyPresetAsync(presetName, script, successMsg);
    }

    private async void ApplyGamingPresetButton_Click(object sender, RoutedEventArgs e)
    {
        var script = @"
        reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR"" /v AllowGameDVR /t REG_DWORD /d 0 /f
        reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU"" /v NoAutoUpdate /t REG_DWORD /d 1 /f
        ";

        string presetName = ResourceString.GetString("GroupPolicyPage_PresetGamingTitle") ?? "Gamers Profile";
        string successMsg = ResourceString.GetString("GroupPolicyPage_PresetGamingSuccess") ?? "Gamers Profile has been applied successfully.";

        await ApplyPresetAsync(presetName, script, successMsg);
    }

    private async Task ApplyPresetAsync(string presetName, string cmdScript, string successMessage)
    {
        try
        {
            ScanProgressRing.Visibility = Visibility.Visible;
            ScanProgressRing.IsActive = true;

            string format = ResourceString.GetString("GroupPolicyPage_PresetApplyingMsg") ?? "Applying {0} and updating Group Policy...";
            SummaryText.Text = string.Format(format, presetName);

            RefreshButton.IsEnabled = false;
            RemoveAllButton.IsEnabled = false;

            await CommandExecutor.RunCommandAsTrustedInstaller(cmdScript, isPowerShell: false);
            await CommandExecutor.InvokeRunCommand("gpupdate /force", isPowerShell: false);

            await ScanPoliciesAsync();
            App.ShowNotification(presetName, successMessage, InfoBarSeverity.Success, 3000);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
    }
    #endregion

    #region State Diffing & Baseline Comparisons
    private async void CompareBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        if (_policyStates == null) return;

        var anomalies = _policyStates.Where(s => s.IsConfigured).ToList();

        string title = ResourceString.GetString("GroupPolicyPage_BaselineCompareTitle") ?? "Baseline Comparison";
        string content;

        if (anomalies.Count == 0)
        {
            content = ResourceString.GetString("GroupPolicyPage_BaselineClean") ??
                "Your system matches the Vanilla Windows 11 baseline. No altered policies detected.";
        }
        else
        {
            string format = ResourceString.GetString("GroupPolicyPage_BaselineAnomalies") ??
                "Detected {0} policies that deviate from the standard Windows baseline. These have been altered by corporate domains, malware, or tweaking tools.";
            content = string.Format(format, anomalies.Count);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
            Title = title,
            Content = content,
            CloseButtonText = ResourceString.GetString("Close") ?? "Close"
        };

        await dialog.ShowAsync();
    }
    #endregion

    #region Custom Policy Injection (Manual Overrides)
    private async void AddCustomOverrideButton_Click(object sender, RoutedEventArgs e)
    {
        var hiveCombo = new ComboBox { ItemsSource = new[] { "HKEY_LOCAL_MACHINE", "HKEY_CURRENT_USER" }, SelectedIndex = 0, Width = 300, Margin = new Thickness(0, 0, 0, 8) };
        var keyPathInput = new TextBox { PlaceholderText = @"SOFTWARE\Policies\...", Width = 300, Margin = new Thickness(0, 0, 0, 8) };
        var valueNameInput = new TextBox { PlaceholderText = "Value Name", Width = 300, Margin = new Thickness(0, 0, 0, 8) };
        var typeCombo = new ComboBox { ItemsSource = new[] { "REG_DWORD", "REG_SZ" }, SelectedIndex = 0, Width = 300, Margin = new Thickness(0, 0, 0, 8) };
        var valueInput = new TextBox { PlaceholderText = "Value Data (e.g., 0, 1, or text)", Width = 300, Margin = new Thickness(0, 0, 0, 8) };

        var formPanel = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "Registry Hive:", Margin = new Thickness(0, 0, 0, 4) }, hiveCombo,
                new TextBlock { Text = "Key Path:", Margin = new Thickness(0, 0, 0, 4) }, keyPathInput,
                new TextBlock { Text = "Value Name:", Margin = new Thickness(0, 0, 0, 4) }, valueNameInput,
                new TextBlock { Text = "Value Type:", Margin = new Thickness(0, 0, 0, 4) }, typeCombo,
                new TextBlock { Text = "Value Data:", Margin = new Thickness(0, 0, 0, 4) }, valueInput
            }
        };

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
            Title = ResourceString.GetString("GroupPolicyPage_CustomOverrideTitle") ?? "Create Custom Policy Override",
            Content = formPanel,
            PrimaryButtonText = ResourceString.GetString("GroupPolicyPage_Inject") ?? "Inject Policy",
            CloseButtonText = ResourceString.GetString("Cancel") ?? "Cancel"
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            string hive = hiveCombo.SelectedIndex == 0 ? "HKLM" : "HKCU";
            string keyPath = keyPathInput.Text.Trim();
            string valueName = valueNameInput.Text.Trim();
            string type = typeCombo.SelectedIndex == 0 ? "REG_DWORD" : "REG_SZ";
            string value = valueInput.Text.Trim();

            if (string.IsNullOrEmpty(keyPath) || string.IsNullOrEmpty(valueName) || string.IsNullOrEmpty(value))
            {
                App.ShowNotification("Injection Error", "All policy fields are required to inject a custom override.", InfoBarSeverity.Error, 3000);
                return;
            }

            try
            {
                ScanProgressRing.Visibility = Visibility.Visible;
                ScanProgressRing.IsActive = true;
                SummaryText.Text = "Injecting custom policy...";
                RefreshButton.IsEnabled = false;
                RemoveAllButton.IsEnabled = false;

                string script = $"reg add \"{hive}\\{keyPath}\" /v \"{valueName}\" /t {type} /d \"{value}\" /f";

                await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: false);
                await CommandExecutor.InvokeRunCommand("gpupdate /force", isPowerShell: false);

                await ScanPoliciesAsync();
                App.ShowNotification("Success", "Custom policy successfully injected and tracked.", InfoBarSeverity.Success, 3000);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }
    }
    #endregion

    #region AI Explainer
    private async void ExplainPolicy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PolicyStateViewModel vm)
        {
            var flyout = button.Flyout as Flyout;
            if (flyout == null) return;

            var stackPanel = flyout.Content as StackPanel;
            var textBlock = stackPanel?.Children.OfType<TextBlock>().FirstOrDefault(x => x.Name == "AiExplanationText");

            if (textBlock == null) return;

            textBlock.Text = ResourceString.GetString("ai_explainer_thinking") ?? "Thinking...";

            string context = $"Name: {vm.Policy.Name}\n" +
                             $"Category: {vm.Policy.Category}\n" +
                             $"Path: {vm.Policy.RegistryPath}\n" +
                             $"Value Name: {vm.Policy.ValueName}\n" +
                             $"{vm.CurrentValueDisplay}";

            string category = ResourceString.GetString("group_policy_page_category_name") ?? "Group Policy";

            string explanation = await AiExplainerService.ExplainGenericItemAsync(
                itemName: vm.Policy.Name,
                itemCategory: category,
                contextDetails: context
            );

            textBlock.Text = explanation;
        }
    }

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
    #endregion

    #region Removal & Refresh Logic
    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ScanPoliciesAsync();
    }

    private async void RemoveAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_policyStates == null) return;

        var configuredPolicies = _policyStates.Where(s => s.IsConfigured).ToList();
        if (configuredPolicies.Count == 0) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
            Title = ResourceString.GetString("GroupPolicyPage_ConfirmRemoveAllTitle"),
            Content = string.Format(ResourceString.GetString("GroupPolicyPage_ConfirmRemoveAllContent") ?? "Remove {0} policies?", configuredPolicies.Count),
            PrimaryButtonText = ResourceString.GetString("GroupPolicyPage_Remove") ?? "Remove",
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
            CloseButtonText = ResourceString.GetString("Cancel") ?? "Cancel"
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await RemovePoliciesAsync(configuredPolicies.Select(s => s.Policy));
    }

    private async void CategoryRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string category) return;
        if (_policyStates == null) return;

        var categoryPolicies = _policyStates.Where(s => s.IsConfigured && s.Policy.Category == category).ToList();
        if (categoryPolicies.Count == 0) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
            Title = ResourceString.GetString("GroupPolicyPage_ConfirmRemoveCategoryTitle"),
            Content = string.Format(ResourceString.GetString("GroupPolicyPage_ConfirmRemoveCategoryContent") ?? "Remove {0} policies in {1}?", categoryPolicies.Count, category),
            PrimaryButtonText = ResourceString.GetString("GroupPolicyPage_Remove") ?? "Remove",
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
            CloseButtonText = ResourceString.GetString("Cancel") ?? "Cancel"
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await RemovePoliciesAsync(categoryPolicies.Select(s => s.Policy));
    }

    private async void PolicyRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string policyId) return;
        if (_policyStates == null) return;

        var policy = _policyStates.FirstOrDefault(s => s.Policy.Id == policyId);
        if (policy == null) return;

        await RemovePoliciesAsync([policy.Policy]);
    }

    private async void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ConfiguredPoliciesListView.SelectedItems.OfType<PolicyStateViewModel>().ToList();
        if (selectedItems.Count == 0) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
            Title = ResourceString.GetString("GroupPolicyPage_ConfirmRemoveSelectedTitle"),
            Content = string.Format(ResourceString.GetString("GroupPolicyPage_ConfirmRemoveSelectedContent") ?? "Remove {0} selected policies?", selectedItems.Count),
            PrimaryButtonText = ResourceString.GetString("GroupPolicyPage_Remove") ?? "Remove",
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
            CloseButtonText = ResourceString.GetString("Cancel") ?? "Cancel"
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await RemovePoliciesAsync(selectedItems.Select(s => s.Policy));
    }

    private async Task RemovePoliciesAsync(IEnumerable<GroupPolicyHelper.PolicyEntry> policies)
    {
        var policyList = policies.ToList();
        if (policyList.Count == 0) return;

        try
        {
            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            ScanProgressRing.Visibility = Visibility.Visible;
            ScanProgressRing.IsActive = true;
            SummaryText.Text = ResourceString.GetString("GroupPolicyPage_RemovingPolicies") ?? "Removing policies...";
            RefreshButton.IsEnabled = false;
            RemoveAllButton.IsEnabled = false;

            var (succeeded, failed) = await GroupPolicyHelper.RemovePolicyOverridesAsync(policyList);

            if (succeeded > 0)
            {
                SummaryText.Text = ResourceString.GetString("GroupPolicyPage_UpdatingPoliciesMsg") ?? "Updating Windows Policies (gpupdate /force)...";
                await CommandExecutor.InvokeRunCommand("gpupdate /force", isPowerShell: false);

                var restartDialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                    BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                    Title = ResourceString.GetString("GroupPolicyPage_RestartExplorerTitle"),
                    Content = ResourceString.GetString("GroupPolicyPage_RestartExplorerContent"),
                    PrimaryButtonText = ResourceString.GetString("GroupPolicyPage_RestartNow") ?? "Restart Now",
                    PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                    CloseButtonText = ResourceString.GetString("GroupPolicyPage_Later") ?? "Later"
                };

                var restartResult = await restartDialog.ShowAsync();
                if (restartResult == ContentDialogResult.Primary)
                {
                    await GroupPolicyHelper.RestartExplorerAsync();
                }
            }

            await ScanPoliciesAsync();
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
        finally
        {
            ScanProgressRing.Visibility = Visibility.Collapsed;
            ScanProgressRing.IsActive = false;
            RefreshButton.IsEnabled = true;

            EfficiencyModeHelper.IsUIWakeLockActive = false;
            if (LocalMachineSettingsEngine.RunOnPriority == Core.Enums.Priority.Low)
            {
                EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
            }
        }
    }
    #endregion

    #region Purge Page
    public async Task Purge()
    {
        Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

        if (_cancellationTokenSource != null)
        {
            try
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            catch (ObjectDisposedException) { }
            _cancellationTokenSource = null;
        }

        if (!SettingsEngine.IsHighPerformanceModeEnabled)
        {
            Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and Collections...");

            _policyStates = null;

            ConfiguredPoliciesListView.ItemsSource = null;
            CategorySummaryRepeater.ItemsSource = null;

            this.Loaded -= GroupPolicyPage_Loaded;
            this.Unloaded -= GroupPolicyPage_Unloaded;
            ConfiguredPoliciesListView.SelectionChanged -= ConfiguredPoliciesListView_SelectionChanged;

            this.DataContext = null;
            this.Content = null;
            //this.Bindings?.StopTracking();

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