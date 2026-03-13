// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Net.NetworkInformation;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.Settings;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using static EvolveOS_Optimizer.Core.Enums;
using static EvolveOS_Optimizer.Utilities.Managers.NotificationManager;
using ComboBoxItem = EvolveOS_Optimizer.Core.Structs.ComboBoxItem;

namespace EvolveOS_Optimizer.Pages
{
    public partial class DNSChangerPage : Page
    {
        #region Variables & Setup
        private readonly Dictionary<IDNSCryptSetting, ComboBox> _controls;

        public DNSChangerPage()
        {
            InitializeComponent();

            CmbDnsPresets.ItemsSource = DnsPreset.DefaultPresets;
            this.Loaded += DNSChangerPage_Loaded;

            _controls = new Dictionary<IDNSCryptSetting, ComboBox>
            {
                {new DNSCryptSetting_ipv4_servers(), ipv4_servers},
                {new DNSCryptSetting_ipv6_servers(), ipv6_servers},
                {new DNSCryptSetting_dnscrypt_servers(), dnscrypt_servers},
                {new DNSCryptSetting_doh_servers(), doh_servers},
                {new DNSCryptSetting_require_dnssec(), require_dnssec},
                {new DNSCryptSetting_require_nolog(), require_nolog},
                {new DNSCryptSetting_require_nofilter(), require_nofilter},
                {new DNSCryptSetting_bootstrap_resolvers(), bootstrap_resolvers},
                {new DNSCryptSetting_dnscrypt_ephemeral_keys(), dnscrypt_ephemeral_keys},
                {new DNSCryptSetting_tls_disable_session_tickets(), tls_disable_session_tickets},
                {new DNSCryptSetting_netprobe_timeout(), netprobe_timeout},
                {new DNSCryptSetting_netprobe_address(), netprobe_address},
                {new DNSCryptSetting_block_ipv6(), block_ipv6},
                {new DNSCryptSetting_reject_ttl(), reject_ttl},
            };

            BtnDownloadInstall.RenderTransform = new TransformGroup();
            ((TransformGroup)BtnDownloadInstall.RenderTransform).Children.Add(new TranslateTransform());
        }

        private async void DNSChangerPage_Loaded(object sender, RoutedEventArgs e)
        {
            await NetworkHelper.IsConnectedAsync();
            _ = Task.Run(() => UpdateCurrentDnsDisplay());

            UpdateControls();
            AnimateInstallButton();
        }
        #endregion

        #region DNS Changer Logic
        private async Task UpdateCurrentDnsDisplay()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                string retrieving = ResourceString.GetString("status_retrieving");
                TxtCurrentIpv4.Text = string.Format(ResourceString.GetString("label_ipv4_status"), retrieving);
                TxtCurrentIpv6.Text = string.Format(ResourceString.GetString("label_ipv6_status"), retrieving);
            });

            await Task.Delay(100);

            try
            {
                DnsManager dnsManager = new DnsManager();

                string currentIpv4Primary = dnsManager.GetCurrentIpv4Primary();
                string currentIpv4Secondary = dnsManager.GetCurrentIpv4Secondary();
                string currentIpv6Primary = dnsManager.GetCurrentIpv6Primary();
                string currentIpv6Secondary = dnsManager.GetCurrentIpv6Secondary();

                string autoText = ResourceString.GetString("status_automatic_none");

                string ipv4Display = (string.IsNullOrWhiteSpace(currentIpv4Primary) || currentIpv4Primary == "0.0.0.0")
                                      ? autoText
                                      : $"{currentIpv4Primary} / {currentIpv4Secondary}";

                string ipv6Display = (string.IsNullOrWhiteSpace(currentIpv6Primary) || currentIpv6Primary == "::")
                                      ? autoText
                                      : $"{currentIpv6Primary} / {currentIpv6Secondary}";

                DispatcherQueue.TryEnqueue(() =>
                {
                    TxtCurrentIpv4.Text = string.Format(ResourceString.GetString("label_ipv4_status"), ipv4Display);
                    TxtCurrentIpv6.Text = string.Format(ResourceString.GetString("label_ipv6_status"), ipv6Display);

                    SyncComboBoxWithSystemDns(currentIpv4Primary, currentIpv4Secondary);
                });
            }
            catch (Exception)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    string errorText = ResourceString.GetString("status_failed_to_load");
                    TxtCurrentIpv4.Text = string.Format(ResourceString.GetString("label_ipv4_status"), errorText);
                    TxtCurrentIpv6.Text = string.Format(ResourceString.GetString("label_ipv6_status"), errorText);
                });
            }
        }

        private void SyncComboBoxWithSystemDns(string primary, string secondary)
        {
            var matchingPreset = DnsPreset.DefaultPresets.FirstOrDefault(p =>
                p.Ipv4Primary != "0.0.0.0" &&
                p.Name != "Custom" &&
                string.Equals(p.Ipv4Primary, primary, StringComparison.OrdinalIgnoreCase));

            if (matchingPreset != null)
            {
                CmbDnsPresets.SelectedItem = matchingPreset;
            }
            else if (string.IsNullOrEmpty(primary) || primary == "0.0.0.0")
            {
                CmbDnsPresets.SelectedIndex = 0;
            }
            else
            {
                var customPreset = DnsPreset.DefaultPresets.FirstOrDefault(p => p.Name == "Custom");
                if (customPreset != null)
                {
                    CmbDnsPresets.SelectedItem = customPreset;
                    TxtIpv4Primary.Text = primary;
                    TxtIpv4Secondary.Text = secondary;
                }
            }
        }

        private async void BtnTestLatency_Click(object sender, RoutedEventArgs e)
        {
            var selectedDns = (DnsPreset)CmbDnsPresets.SelectedItem;

            if (selectedDns == null || selectedDns.Name == "Automatic")
            {
                TxtPingResult.Text = "N/A";
                TxtPingResult.Foreground = new SolidColorBrush(Colors.Gray);
                return;
            }

            string? targetIp = (selectedDns.Name == "Custom") ? TxtIpv4Primary.Text : selectedDns.Ipv4Primary;
            if (string.IsNullOrWhiteSpace(targetIp))
            {
                return;
            }

            BtnTestLatency.IsEnabled = false;
            TxtPingResult.Text = ResourceString.GetString("status_pinging");

            TxtPingResult.Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];

            try
            {
                long latency = await Task.Run(() => PerformPing(targetIp));

                if (latency >= 0)
                {
                    TxtPingResult.Text = $"{latency} ms";

                    if (latency < 50)
                    {
                        TxtPingResult.Foreground = new SolidColorBrush(Colors.LightGreen);
                    }
                    else if (latency < 100)
                    {
                        TxtPingResult.Foreground = new SolidColorBrush(Colors.Orange);
                    }
                    else
                    {
                        TxtPingResult.Foreground = new SolidColorBrush(Colors.Salmon);
                    }
                }
                else
                {
                    TxtPingResult.Text = ResourceString.GetString("status_ping_timeout");
                    TxtPingResult.Foreground = new SolidColorBrush(Colors.Gray);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DNS Test] Error: {ex.Message}");
                TxtPingResult.Text = "Error";
            }
            finally
            {
                BtnTestLatency.IsEnabled = true;
            }
        }

        private async Task<long> PerformPing(string ip)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(ip, 2000);
                    if (reply.Status == IPStatus.Success)
                    {
                        return reply.RoundtripTime;
                    }
                }
            }
            catch { }
            return -1;
        }

        private async void BtnFindBestServer_Click(object sender, RoutedEventArgs e)
        {
            BtnFindBestServer.IsEnabled = false;
            BtnTestLatency.IsEnabled = false;
            TxtPingResult.Text = "...";
            TxtPingResult.Foreground = new SolidColorBrush(Colors.Gray);

            try
            {
                bool isConnected = await Task.Run(() => NetworkHelper.IsConnectedAsync());

                if (!isConnected)
                {
                    TxtPingResult.Text = "No Link";
                    return;
                }

                var testablePresets = DnsPreset.DefaultPresets
                    .Where(p => p.Name != "Automatic" && p.Name != "Custom" && p.Name != "DNSCrypt" && !string.IsNullOrEmpty(p.Ipv4Primary))
                    .ToList();

                DnsPreset? bestPreset = null;
                long minLatency = long.MaxValue;

                var pingTasks = testablePresets.Select(async preset =>
                {
                    long latency = await PerformPing(preset.Ipv4Primary!);
                    return new { Preset = preset, Latency = latency };
                });

                var results = await Task.WhenAll(pingTasks);

                foreach (var result in results)
                {
                    if (result.Latency > 0 && result.Latency < minLatency)
                    {
                        minLatency = result.Latency;
                        bestPreset = result.Preset;
                    }
                }

                if (bestPreset != null)
                {
                    CmbDnsPresets.SelectedItem = bestPreset;
                    TxtPingResult.Text = $"{minLatency} ms";
                    TxtPingResult.Foreground = new SolidColorBrush(Colors.LightGreen);

                    string successFormat = ResourceString.GetString("noty_best_dns_found");
                    string messageBody = string.Format(successFormat, bestPreset.Name, minLatency);

                    NotificationManager.Show("DNS Optimizer", messageBody)
                        .WithSeverity(NoticeSeverity.Info)
                        .WithDuration(5000)
                        .Create();
                }
                else
                {
                    TxtPingResult.Text = "N/A";

                    var messageTextBlock = new TextBlock
                    {
                        Text = ResourceString.GetString("msg_best_dns_error_body"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 10, 0, 0)
                    };

                    var errorDialog = new ContentDialog()
                    {
                        XamlRoot = this.XamlRoot,
                        Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                        BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                        Title = ResourceString.GetString("msg_best_dns_error_title"),
                        Content = messageTextBlock,
                        CloseButtonText = ResourceString.GetString("btn_close")
                    };

                    await errorDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DNS Optimizer] Error: {ex.Message}");
            }
            finally
            {
                BtnFindBestServer.IsEnabled = true;
                BtnTestLatency.IsEnabled = true;
            }
        }

        private void CmbDnsPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedDns = (DnsPreset)CmbDnsPresets.SelectedItem;
            if (selectedDns == null) return;

            bool isCustom = selectedDns.Name == "Custom";

            CustomDnsFields.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            TxtIpv4Primary.IsReadOnly = !isCustom;
            TxtIpv4Secondary.IsReadOnly = !isCustom;
            TxtIpv6Primary.IsReadOnly = !isCustom;
            TxtIpv6Secondary.IsReadOnly = !isCustom;

            TxtIpv4Primary.Text = selectedDns.Ipv4Primary;
            TxtIpv4Secondary.Text = selectedDns.Ipv4Secondary;
            TxtIpv6Primary.Text = selectedDns.Ipv6Primary;
            TxtIpv6Secondary.Text = selectedDns.Ipv6Secondary;

            TxtPingResult.Text = "-- ms";
            TxtPingResult.Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
        }

        private async void BtnApplyDNS_Click(object sender, RoutedEventArgs e)
        {
            BtnApplyDNS.IsEnabled = false;

            string ipv4Primary = TxtIpv4Primary.Text;
            string ipv4Secondary = TxtIpv4Secondary.Text;
            string ipv6Primary = TxtIpv6Primary.Text;
            string ipv6Secondary = TxtIpv6Secondary.Text;

            bool successV4 = false;
            bool successV6 = false;

            await Task.Run(() =>
            {
                DnsManager dnsManager = new DnsManager();

                if (!string.IsNullOrWhiteSpace(ipv4Primary))
                {
                    successV4 = dnsManager.SetIpv4Dns(ipv4Primary, ipv4Secondary);
                }

                if (!string.IsNullOrWhiteSpace(ipv6Primary) && ipv6Primary != "::")
                {
                    successV6 = dnsManager.SetIpv6Dns(ipv6Primary, ipv6Secondary);
                }
                else
                {
                    successV6 = true;
                }

                if (successV4 || successV6)
                {
                    NetHelper.FlushDns();
                }
            });

            await UpdateCurrentDnsDisplay();

            if (successV4 && successV6)
            {
                string dnsSuccess = ResourceString.GetString("noty_dns_set_successful");
                NotificationManager.Show("Success", dnsSuccess)
                    .WithSeverity(NoticeSeverity.Success)
                    .WithDuration(5000)
                    .Create();
            }
            else
            {
                var messageTextBlock = new TextBlock
                {
                    Text = ResourceString.GetString("msg_dns_set_error"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var errorDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                    BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                    Title = ResourceString.GetString("error_title") ?? "Error",
                    Content = messageTextBlock,
                    CloseButtonText = ResourceString.GetString("btn_close")
                };

                await errorDialog.ShowAsync();
            }

            BtnApplyDNS.IsEnabled = true;
        }
        #endregion

        #region DNSCrypt Logic
        private void Control_Unloaded(object sender, RoutedEventArgs e)
        {
            // For cleanup if needed
        }

        public void UpdateControls()
        {
            BtnDownloadInstall.IsEnabled = true;

            BtnStartService.Content = "Start service";
            BtnStartService.IsEnabled = true;

            statusLabel.Text = "Nothing is running in the background";
            ProgressRingRunServices.Visibility = Visibility.Collapsed;
            TxtServicesRunning.Text = "";

            IconServiceStopped.Visibility = Visibility.Visible;
            ImgServiceRunning.Visibility = Visibility.Collapsed;

            BtnOpenConfigFile.IsEnabled = true;
            BtnDebug.IsEnabled = true;

            foreach (var pair in _controls)
            {
                pair.Value.IsEnabled = true;
            }

            BtnBalanced.IsEnabled = true;
            BtnPrivacy.IsEnabled = true;
            BtnSaveConfig.IsEnabled = true;
            BtnLoadConfig.IsEnabled = true;

            if (!DNSCryptHelper.IsInstalled())
            {
                BtnStartService.IsEnabled = false;
                BtnOpenConfigFile.IsEnabled = false;
                BtnDebug.IsEnabled = false;

                foreach (var pair in _controls)
                {
                    pair.Value.IsEnabled = false;
                }

                BtnBalanced.IsEnabled = false;
                BtnPrivacy.IsEnabled = false;
                BtnSaveConfig.IsEnabled = false;
                BtnLoadConfig.IsEnabled = false;

                string install = ResourceString.GetString("btn_download_install");
                ToolTipService.SetToolTip(BtnDownloadInstall, install);
                IconDownloadInstall.Glyph = "\uE896";
                IconDownloadInstall.ClearValue(FontIcon.ForegroundProperty);
                AnimateInstallButton();
            }
            else
            {
                string uninstall = ResourceString.GetString("btn_uninstall_script");
                ToolTipService.SetToolTip(BtnDownloadInstall, uninstall);
                IconDownloadInstall.Glyph = "\uE74D";
                IconDownloadInstall.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);

                ((TranslateTransform)((TransformGroup)BtnDownloadInstall.RenderTransform).Children[0]).Y = 0;

                if (DNSCryptHelper.IsRunning())
                {
                    statusLabel.Text = "DNSCrypt Service is running.";
                    ProgressRingRunServices.Visibility = Visibility.Visible;
                    TxtServicesRunning.Text = ResourceString.GetString("text_services_running");

                    IconServiceStopped.Visibility = Visibility.Collapsed;
                    ImgServiceRunning.Visibility = Visibility.Visible;

                    BtnDownloadInstall.IsEnabled = false;
                    BtnStartService.Content = "Stop service";
                    BtnOpenConfigFile.IsEnabled = false;
                    BtnDebug.IsEnabled = false;

                    foreach (var pair in _controls)
                    {
                        pair.Value.IsEnabled = false;
                    }

                    BtnBalanced.IsEnabled = false;
                    BtnPrivacy.IsEnabled = false;
                    BtnSaveConfig.IsEnabled = false;
                    BtnLoadConfig.IsEnabled = false;
                }

                var config = DNSCryptHelper.LoadConfig();

                foreach (var pair in _controls)
                {
                    var currentSetting = pair.Key.GetCurrentSetting(config);
                    var settings = pair.Key.GetSettings(config);

                    pair.Value.Items.Clear();

                    var selectedItem = (object?)null;

                    foreach (var item in settings)
                    {
                        pair.Value.Items.Add(item);

                        if ((string)item.Value == currentSetting)
                        {
                            selectedItem = item;
                        }
                    }

                    if (selectedItem != null)
                    {
                        pair.Value.SelectedItem = selectedItem;
                    }
                    else
                    {
                        pair.Value.SelectedIndex = 0;
                    }
                }
            }
        }

        private void AnimateInstallButton()
        {
            if (!DNSCryptHelper.IsInstalled())
            {
                // FactoryAnimation.ButtonBounce(BtnDownloadInstall, 20, animationDurationSeconds: 0.25);
            }
        }

        private async void ToggleCategoryCards_Click(object sender, RoutedEventArgs e)
        {
            if (ToggleCategoryCards.IsChecked == true)
            {
                ToggleCategoryIcon.Glyph = "\uE70E";
                CategoryCardsContainer.Visibility = Visibility.Visible;
                ExpandCategoryCardsStoryboard.Begin();
            }
            else
            {
                ToggleCategoryIcon.Glyph = "\uE70D";
                CollapseCategoryCardsStoryboard.Begin();

                await Task.Delay(200);
                CategoryCardsContainer.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnDownloadInstall_Click(object sender, RoutedEventArgs e)
        {
            bool isInstalled = DNSCryptHelper.IsInstalled();

            BtnDownloadInstall.IsEnabled = false;

            try
            {
                if (!isInstalled)
                {
                    bool isConnected = await Task.Run(() => NetworkHelper.IsConnectedAsync());
                    if (!isConnected)
                    {
                        return;
                    }
                }

                if (isInstalled)
                {
                    DNSCryptHelper.Uninstall(progressBar, statusLabel);
                    ClearComboBoxes();
                }
                else
                {
                    await DNSCryptHelper.Install(progressBar, statusLabel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DNSCrypt] Operation failed: {ex.Message}");
            }
            finally
            {
                UpdateControls();
                AnimateInstallButton();
                BtnDownloadInstall.IsEnabled = true;
            }
        }

        private void BtnDownloadInstall_MouseEnter(object sender, RoutedEventArgs e)
        {
        }

        private void BtnDownloadInstall_MouseLeave(object sender, RoutedEventArgs e)
        {
        }

        private async void BtnOpenConfigFile_Click(object sender, RoutedEventArgs e)
        {
            BtnOpenConfigFile.IsEnabled = false;
            await DNSCryptHelper.OpenConfig();
            BtnOpenConfigFile.IsEnabled = true;
        }

        private async void BtnStartService_Click(object sender, RoutedEventArgs e)
        {
            BtnStartService.IsEnabled = false;

            try
            {
                if (DNSCryptHelper.IsRunning())
                {
                    await DNSCryptHelper.StopService(progressBar, statusLabel);
                    ProgressRingRunServices.Visibility = Visibility.Collapsed;
                    TxtServicesRunning.Text = "";

                    IconServiceStopped.Visibility = Visibility.Visible;
                    ImgServiceRunning.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UpdateControls();

                    BtnSaveConfig_Click(BtnSaveConfig, null!);

                    await DNSCryptHelper.StartService(progressBar, statusLabel);
                    ProgressRingRunServices.Visibility = Visibility.Visible;
                    TxtServicesRunning.Text = ResourceString.GetString("text_services_running");

                    IconServiceStopped.Visibility = Visibility.Collapsed;
                    ImgServiceRunning.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DNSCrypt Service] Error: {ex.Message}");
                statusLabel.Text = "Service action failed.";
            }
            finally
            {
                UpdateControls();
                BtnStartService.IsEnabled = true;
            }
        }

        private async void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            BtnDebug.IsEnabled = false;

            try
            {
                bool isConnected = await Task.Run(() => NetworkHelper.IsConnectedAsync());

                if (!isConnected)
                {
                    statusLabel.Text = "Connection failed.";
                    return;
                }

                await DNSCryptHelper.DebugProcess(progressBar, statusLabel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DNSCrypt Debug] Error: {ex.Message}");
            }
            finally
            {
                BtnDebug.IsEnabled = true;
            }
        }

        private void BtnBalanced_Click(object sender, RoutedEventArgs e)
        {
            foreach (var pair in _controls)
            {
                var setting = (ComboBoxItem)pair.Value.SelectedItem;
                var targetSetting = pair.Key.GetSetting();

                if ((string)setting.Value != targetSetting)
                {
                    foreach (var item in pair.Value.Items)
                    {
                        setting = (ComboBoxItem)item;

                        if ((string)setting.Value == targetSetting)
                        {
                            pair.Value.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void BtnPrivacy_Click(object sender, RoutedEventArgs e)
        {
            foreach (var pair in _controls)
            {
                var setting = (ComboBoxItem)pair.Value.SelectedItem;
                var targetSetting = pair.Key.GetSetting(DNSSettingPreference.Privacy);

                if ((string)setting.Value != targetSetting)
                {
                    foreach (var item in pair.Value.Items)
                    {
                        setting = (ComboBoxItem)item;

                        if ((string)setting.Value == targetSetting)
                        {
                            pair.Value.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            var config = DNSCryptHelper.LoadConfig();

            foreach (var pair in _controls)
            {
                var setting = (ComboBoxItem)pair.Value.SelectedItem;
                config = pair.Key.SetSetting(config, (string)setting.Value);
            }

            DNSCryptHelper.SaveConfig(config);
        }

        private void BtnLoadConfig_Click(object sender, RoutedEventArgs e)
        {
            UpdateControls();
        }

        private void ClearComboBoxes()
        {
            foreach (var control in _controls.Values)
            {
                control.SelectedItem = null;
            }
        }
        #endregion
    }
}