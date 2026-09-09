// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Security;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using Microsoft.UI.Text;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class DeviceIdentityManagerPage : Page
    {
        private string? _username;
        private SecureString? _masterPassword;

        public DeviceIdentityManagerPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ValueTuple<string, SecureString> navParams)
            {
                _username = navParams.Item1;
                _masterPassword = navParams.Item2;
            }

            await RunInitialStatusCheckAsync();
        }

        #region Dialog & Execution Helpers

        private async Task<bool> ShowStepDialogAsync(string titleKey, string defaultTitle, string messageKey, string defaultMessage, string primaryBtnKey, string defaultPrimaryBtn)
        {
            if (this.XamlRoot == null) return false;

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString(titleKey) ?? defaultTitle,
                Content = new TextBlock
                {
                    Text = ResourceString.GetString(messageKey) ?? defaultMessage,
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = ResourceString.GetString(primaryBtnKey) ?? defaultPrimaryBtn,
                CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private async Task RunInitialStatusCheckAsync()
        {
            try
            {
                var scanResult = await DeviceIdentityEngine.InspectStatusDetailedAsync();
                UpdateStatusBadges(scanResult);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Initial status check failed: {ex.Message}");
            }
        }

        private async Task ShowResultDialogAsync(string title, string content)
        {
            if (this.XamlRoot == null) return;

            var appFont = GetAppCustomFont();

            var sv = new ScrollViewer
            {
                MaxHeight = 400,
                Content = new TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = appFont,
                    FontSize = 12
                }
            };

            var dialog = new ContentDialog
            {
                Title = title,
                Content = sv,
                CloseButtonText = ResourceString.GetString("btn_close") ?? "Close",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async Task ShowResultDialogAsync(string title, IdentityScanResult scanResult)
        {
            if (this.XamlRoot == null) return;

            var appFont = GetAppCustomFont();

            var stack = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 16, 0), HorizontalAlignment = HorizontalAlignment.Stretch };

            var hostsBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var hostsStack = new StackPanel { Spacing = 4 };

            string hostsTitle = scanResult.IsHostsBlocked
                ? (ResourceString.GetString("Degdid_Status_HostsActive") ?? "🛡️ Registration Protection: ACTIVE")
                : (ResourceString.GetString("Degdid_Status_HostsInactive") ?? "⚠️ Registration Protection: NOT CONFIGURED");

            string hostsDesc = scanResult.IsHostsBlocked
                ? (ResourceString.GetString("Degdid_Status_HostsActiveDesc") ?? "Tracking endpoints are successfully redirected to loopback.")
                : (ResourceString.GetString("Degdid_Status_HostsInactiveDesc") ?? "Endpoints are currently open to standard telemetry collection.");

            hostsStack.Children.Add(new TextBlock { Text = hostsTitle, FontWeight = FontWeights.SemiBold, FontFamily = appFont });
            hostsStack.Children.Add(new TextBlock { Text = hostsDesc, FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, FontFamily = appFont });
            hostsBorder.Child = hostsStack;
            stack.Children.Add(hostsBorder);

            string foundCountText = string.Format(ResourceString.GetString("Degdid_Status_FoundCount") ?? "Discovered Identifiers ({0}):", scanResult.FoundTokens.Count);
            stack.Children.Add(new TextBlock
            {
                Text = foundCountText,
                FontWeight = FontWeights.SemiBold,
                FontFamily = appFont,
                Margin = new Thickness(0, 4, 0, 0)
            });

            if (scanResult.FoundTokens.Count == 0)
            {
                string cleanMsg = ResourceString.GetString("Degdid_Status_CleanMessage") ?? "✨ Clean! No active Global Device Identifiers or PUID tokens were found in inspected registry hives.";
                stack.Children.Add(new TextBlock
                {
                    Text = cleanMsg,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Colors.LimeGreen),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = appFont
                });
            }
            else
            {
                string hiveLabel = ResourceString.GetString("Degdid_Status_HiveLabel") ?? "Hive: ";
                string typeLabel = ResourceString.GetString("Degdid_Status_TypeLabel") ?? "Type: ";

                foreach (var token in scanResult.FoundTokens)
                {
                    var tokenCard = new Border
                    {
                        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(12),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    var tokenPanel = new StackPanel { Spacing = 2 };
                    tokenPanel.Children.Add(new TextBlock { Text = $"{hiveLabel}{token.HiveName}", FontWeight = FontWeights.SemiBold, FontSize = 12, FontFamily = appFont });
                    tokenPanel.Children.Add(new TextBlock { Text = $"{typeLabel}{token.TokenType}", FontSize = 11, Opacity = 0.6, FontFamily = appFont });
                    tokenPanel.Children.Add(new TextBlock
                    {
                        Text = token.TokenValue,
                        FontFamily = appFont,
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["MyDynamicAccentBrush"],
                        TextWrapping = TextWrapping.Wrap
                    });

                    tokenCard.Child = tokenPanel;
                    stack.Children.Add(tokenCard);
                }
            }

            var sv = new ScrollViewer
            {
                MaxHeight = 450,
                Content = stack,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Disabled
            };

            var dialog = new ContentDialog
            {
                Title = title,
                Content = sv,
                CloseButtonText = ResourceString.GetString("btn_close") ?? "Close",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async Task<string> ExecuteEngineOperationAsync(Func<Task<string>> operation, string loadingMessage)
        {
            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            LoadingTitleText.Text = loadingMessage;
            UIHelper.SetOverlay(true);
            LoadingOverlay.Visibility = Visibility.Visible;

            string output = string.Empty;

            try
            {
                output = await operation();
            }
            catch (Exception ex)
            {
                output = $"Execution failed with error:\n{ex.Message}\n{ex.StackTrace}";
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                UIHelper.SetOverlay(false);

                EfficiencyModeHelper.IsUIWakeLockActive = false;
                if (LocalMachineSettingsEngine.RunOnPriority == Core.Enums.Priority.Low)
                {
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
                }
            }

            return output;
        }

        private FontFamily GetAppCustomFont()
        {
            try
            {
                if (LoadingTitleText != null && LoadingTitleText.FontFamily != null)
                {
                    return LoadingTitleText.FontFamily;
                }
            }
            catch { }

            return new FontFamily("Segoe UI");
        }

        #endregion

        #region UI Button Handlers

        private async void BtnCheckStatus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var scanResult = await DeviceIdentityEngine.InspectStatusDetailedAsync();

                UpdateStatusBadges(scanResult);

                await ShowResultDialogAsync("Device Identity Diagnostics", scanResult);
            }
            catch (Exception ex)
            {
                NotificationManager.Show(ResourceString.GetString("Toast_Error_Title") ?? "Error", ex.Message)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
        }

        private void UpdateStatusBadges(IdentityScanResult scanResult)
        {
            if (scanResult.IsHostsBlocked)
            {
                BadgeProtection.Style = (Style)Application.Current.Resources["BadgeRecommendedStyle"];
                TxtBadgeProtection.Text = "Protection: Active";

                BadgeHosts.Style = (Style)Application.Current.Resources["BadgeRecommendedStyle"];
                TxtBadgeHosts.Text = "Hosts/Firewall: Blocked";
            }
            else
            {
                BadgeProtection.Style = (Style)Application.Current.Resources["BadgeNonReinstallableStyle"];
                TxtBadgeProtection.Text = "Protection: Inactive";

                BadgeHosts.Style = (Style)Application.Current.Resources["BadgeDefaultStyle"];
                TxtBadgeHosts.Text = "Hosts/Firewall: Open";
            }

            if (scanResult.FoundTokens.Count == 0)
            {
                BadgeCount.Style = (Style)Application.Current.Resources["BadgeRecommendedStyle"];
                TxtBadgeCount.Text = "Tokens Found: 0 (Clean)";
            }
            else
            {
                BadgeCount.Style = (Style)Application.Current.Resources["BadgeNonReinstallableStyle"];
                TxtBadgeCount.Text = $"Tokens Found: {scanResult.FoundTokens.Count}";
            }

            bool hasDecoy = scanResult.FoundTokens.Exists(t => t.TokenType.Contains("Decoy") || t.TokenValue.StartsWith("0018"));
            if (hasDecoy)
            {
                BadgeDecoy.Style = (Style)Application.Current.Resources["BadgeCustomStyle"];
                TxtBadgeDecoy.Text = "Decoy: Deployed";
            }
            else
            {
                BadgeDecoy.Style = (Style)Application.Current.Resources["BadgeDefaultStyle"];
                TxtBadgeDecoy.Text = "Decoy: Inactive";
            }
        }

        private async void BtnProtectWipe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool continueStep = await ShowStepDialogAsync(
                    "Degdid_Wizard_WipeTitle", "Confirm Permanent Identity Wipe",
                    "Degdid_Wizard_WipeDesc", "This will implement aggressive outbound hosts block against login.live.com and irreversibly purge your Global Device Identifiers (GDID) and identity tokens from registry hives. Certain Microsoft Account features will break.\n\nDo you want to proceed?",
                    "Degdid_Wizard_WipeBtn", "Protect & Wipe");
                if (!continueStep) return;

                string result = await ExecuteEngineOperationAsync(() => DeviceIdentityEngine.ProtectAndWipeAsync(), "Applying Blocks & Wiping Identifiers...");

                NotificationManager.Show(ResourceString.GetString("Toast_Success_Title") ?? "Operation Complete", "Wipe sequence executed. Check the detailed log.")
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();

                await RunInitialStatusCheckAsync();
                await ShowResultDialogAsync("Wipe Results", result);
            }
            catch (Exception ex)
            {
                NotificationManager.Show(ResourceString.GetString("Toast_Error_Title") ?? "Error", ex.Message)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
        }

        private async void BtnProtectDecoy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool continueStep = await ShowStepDialogAsync(
                    "Degdid_Wizard_DecoyTitle", "Confirm Decoy Deployment",
                    "Degdid_Wizard_DecoyDesc", "This is an experimental feature. It will apply registration blocks and inject a dynamically generated fake identifier to spoof telemetry queries. Microsoft services may become unstable.\n\nDo you want to proceed?",
                    "Degdid_Wizard_DecoyBtn", "Deploy Decoy");
                if (!continueStep) return;

                string result = await ExecuteEngineOperationAsync(() => DeviceIdentityEngine.ProtectAndDecoyAsync(), "Spoofing Telemetry & Deploying Decoy...");

                NotificationManager.Show(ResourceString.GetString("Toast_Success_Title") ?? "Operation Complete", "Decoy sequence executed. Check the detailed log.")
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();

                await RunInitialStatusCheckAsync();
                await ShowResultDialogAsync("Decoy Deployment Results", result);
            }
            catch (Exception ex)
            {
                NotificationManager.Show(ResourceString.GetString("Toast_Error_Title") ?? "Error", ex.Message)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
        }

        private async void BtnUnblock_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool continueStep = await ShowStepDialogAsync(
                    "Degdid_Wizard_UnblockTitle", "Confirm Network Unblock",
                    "Degdid_Wizard_UnblockDesc", "This will revert the custom hosts file entries, allowing Windows to communicate with Microsoft tracking endpoints again. It will NOT restore your wiped identifiers.\n\nDo you want to proceed?",
                    "Degdid_Wizard_UnblockBtn", "Unblock Network");
                if (!continueStep) return;

                string result = await ExecuteEngineOperationAsync(() => DeviceIdentityEngine.UnblockNetworkAsync(), "Removing Network Blocks...");

                NotificationManager.Show(ResourceString.GetString("Toast_Success_Title") ?? "Operation Complete", "Network blocks have been lifted.")
                                   .WithSeverity(NotificationManager.NoticeSeverity.Success)
                                   .Create();

                await RunInitialStatusCheckAsync();
                await ShowResultDialogAsync("Unblock Results", result);
            }
            catch (Exception ex)
            {
                NotificationManager.Show(ResourceString.GetString("Toast_Error_Title") ?? "Error", ex.Message)
                                   .WithSeverity(NotificationManager.NoticeSeverity.Error)
                                   .Create();
            }
        }

        #endregion

        #region Navigation

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null)
            {
                if (this.Frame.CanGoBack)
                {
                    this.Frame.GoBack();
                }
                else
                {
                    this.Frame.Navigate(typeof(AdvancedUtilsPage));
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _masterPassword?.Dispose();
        }

        #endregion
    }
}