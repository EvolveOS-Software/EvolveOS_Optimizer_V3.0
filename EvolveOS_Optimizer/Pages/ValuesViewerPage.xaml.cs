// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Dialogs;
using EvolveOS_Optimizer.Helpers;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Xaml.Input;
using Vanara.PInvoke;
using WinRT.Interop;
using static EvolveOS_Optimizer.Core.Enums;
using static Vanara.PInvoke.AdvApi32;
using Microsoft.UI;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class ValuesViewerPage : Page
    {
        #region Fields
        private static readonly HttpClient _http = new HttpClient();
        private CancellationTokenSource? _searchCancellationToken;

        private bool _isDialogOpen = false;
        private bool _isAddressBoxMenuOpen = false;
        #endregion

        #region Properties
        public ValuesViewerViewModel ViewModel { get; set; } = null!;
        #endregion

        #region Constructor
        public ValuesViewerPage()
        {
            this.InitializeComponent();
            this.Loaded += async (s, e) => await InitializeAiStateAsync();

            WeakReferenceMessenger.Default.Register<OpenFindDialogMessage>(this, (r, m) =>
            {
                var page = (ValuesViewerPage)r;

                if (page.XamlRoot == null)
                {
                    App.ShowNotification(
                        ResourceString.GetString("values_viewer_search_setup_title"),
                        ResourceString.GetString("values_viewer_search_setup_msg"),
                        InfoBarSeverity.Informational,
                        3000);
                    return;
                }

                page.OnFindButtonClick(page, new RoutedEventArgs());
            });
        }
        #endregion

        #region Navigation
        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ValuesViewerViewModel passedViewModel)
            {
                this.ViewModel = passedViewModel;
                this.Bindings.Update();
            }
        }
        #endregion

        #region Standard UI Events & Permissions
        private async void ValueListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if ((ValueItem)ValueListView.SelectedItem is not { } item)
                return;

            var dialog = new ValueEditingDialog
            {
                ViewModel = new() { ValueItem = item },
                ParentViewModel = this.ViewModel,
                XamlRoot = this.Content.XamlRoot,
            };

            _ = await dialog.ShowAsync();
        }

        private void ValueListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.SelectedValueItem = (ValueItem)ValueListView.SelectedItem;
        }

        private void OnKeyPermissionsButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var item = ViewModel.SelectedKeyItem;
            if (item != null)
            {
                PropertyWindowHelpers.CreatePropertyWindow(item);
            }
        }

        private async void OnForceUnlock_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var key = ViewModel.SelectedKeyItem;
            if (key == null) return;

            Microsoft.Win32.RegistryKey? dotNetRootKey = GetDotNetRegistryKey(key.RootHive);
            if (dotNetRootKey == null) return;

            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = ResourceString.GetString("values_viewer_force_unlock_title"),
                Content = string.Format(ResourceString.GetString("values_viewer_force_unlock_content"), key.Path),
                PrimaryButtonText = ResourceString.GetString("values_viewer_force_unlock_primary"),
                CloseButtonText = ResourceString.GetString("values_viewer_dialog_cancel"),
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                bool success = RegistryPermissionsManager.GrantUltimateAccess(dotNetRootKey, key.Path);
                ViewModel.StatusBarMessage = success
                    ? ResourceString.GetString("values_viewer_status_unlocked")
                    : ResourceString.GetString("values_viewer_status_unlock_failed");
            }
        }

        private async void OnRestoreOwnership_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var key = ViewModel.SelectedKeyItem;
            if (key == null) return;

            Microsoft.Win32.RegistryKey? dotNetRootKey = GetDotNetRegistryKey(key.RootHive);
            if (dotNetRootKey == null) return;

            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = ResourceString.GetString("values_viewer_restore_title"),
                Content = string.Format(ResourceString.GetString("values_viewer_restore_content"), key.Path),
                PrimaryButtonText = ResourceString.GetString("values_viewer_restore_primary"),
                CloseButtonText = ResourceString.GetString("values_viewer_dialog_cancel"),
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                bool success = RegistryPermissionsManager.RestoreTrustedInstallerOwnership(dotNetRootKey, key.Path);
                ViewModel.StatusBarMessage = success
                    ? ResourceString.GetString("values_viewer_status_restored")
                    : ResourceString.GetString("values_viewer_status_restore_failed");
            }
        }

        private async void OnFindButtonClick(object sender, RoutedEventArgs e)
        {
            if (_isDialogOpen || this.XamlRoot == null) return;

            _isDialogOpen = true;

            try
            {
                var dialog = new FindDialog(ViewModel.SavedSearchOptions)
                {
                    XamlRoot = this.XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    dialog.SaveState();

                    _searchCancellationToken?.Cancel();
                    _searchCancellationToken = new CancellationTokenSource();

                    _ = ViewModel.SearchRegistryAsync(_searchCancellationToken.Token);
                }
            }
            finally
            {
                _isDialogOpen = false;
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CloudConnectDialog
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await InitializeAiStateAsync();
            }

            if (result == ContentDialogResult.Primary)
            {
                var provider = LocalMachineSettingsEngine.ActiveAiProvider;
                string apiKey = GetKeyForProvider(provider);

                if (string.IsNullOrEmpty(apiKey))
                {
                    ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_api_key_req"), provider);
                    ViewModel.ConnectionStatusBrush = new SolidColorBrush(Colors.Red);
                    return;
                }

                ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_connecting"), provider);
                ViewModel.ConnectionStatusBrush = new SolidColorBrush(Colors.Gold);

                bool isConnected = await CheckConnectionAsync(provider);

                if (isConnected)
                {
                    ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_connected_success"), provider);
                    ViewModel.ConnectionStatusBrush = new SolidColorBrush(Colors.LimeGreen);
                }
                else
                {
                    ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_connected_failed"), provider);
                    ViewModel.ConnectionStatusBrush = new SolidColorBrush(Colors.Red);
                }
            }
        }

        private async Task InitializeAiStateAsync()
        {
            var provider = LocalMachineSettingsEngine.ActiveAiProvider;
            string apiKey = GetKeyForProvider(provider);

            ViewModel.ConnectionStatusBrush = new SolidColorBrush(Colors.Gray);
            ViewModel.IsAiReady = false;
            AiExplainerService.IsAiReady = false;

            if (string.IsNullOrEmpty(apiKey))
            {
                ViewModel.StatusBarMessage = ResourceString.GetString("values_viewer_status_ai_disabled");
                return;
            }

            ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_ai_verifying"), provider);
            ViewModel.ConnectionStatusBrush = new SolidColorBrush(Colors.Gold);

            bool isConnected = await CheckConnectionAsync(provider);

            if (isConnected)
            {
                ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_ai_connected"), provider);
                ViewModel.ConnectionStatusBrush = new SolidColorBrush(Colors.LimeGreen);
                ViewModel.IsAiReady = true;
                AiExplainerService.IsAiReady = true;
            }
            else
            {
                ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_ai_conn_failed"), provider);
                ViewModel.ConnectionStatusBrush = new SolidColorBrush(Colors.Red);
                AiExplainerService.IsAiReady = false;
            }
        }

        private void OnSnapshotAClick(object sender, RoutedEventArgs e)
        {
            _ = ViewModel.RunFirstSnapshotAsync();
        }

        private void OnSnapshotBClick(object sender, RoutedEventArgs e)
        {
            _ = ViewModel.RunSecondSnapshotAndCompareAsync();
        }

        private void CloseSnapshot_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsSnapshotActive = false;
            ViewModel.SnapshotResults.Clear();
            ViewModel.StatusBarMessage = ResourceString.GetString("values_viewer_status_ready");
        }

        private void SnapshotResultsListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (SnapshotResultsListView.SelectedItem is RegistryChange selectedChange)
            {
                if (selectedChange.Type == ChangeType.Deleted)
                {
                    App.ShowNotification(
                        ResourceString.GetString("values_viewer_notice_title"),
                        ResourceString.GetString("values_viewer_notice_deleted_snapshot"),
                        InfoBarSeverity.Informational,
                        3000);
                    return;
                }

                ViewModel.IsSnapshotActive = false;

                string targetPath = selectedChange.Path;
                string targetValueName = "";

                if (selectedChange.Type == ChangeType.Modified || selectedChange.Details.StartsWith("Value"))
                {
                    int lastSlash = targetPath.LastIndexOf('\\');
                    if (lastSlash > 0)
                    {
                        targetValueName = targetPath.Substring(lastSlash + 1);
                        targetPath = targetPath.Substring(0, lastSlash);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[RegShot] Requesting navigation to: {targetPath} | Value: {targetValueName}");

                WeakReferenceMessenger.Default.Send(new RegistryNavigationMessage(
                    HKEY.HKEY_CURRENT_USER,
                    targetPath,
                    targetValueName));
            }
        }
        #endregion

        #region AI Event Handlers
        private async void OnAskAiValue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem { DataContext: ValueItem valueItem })
                return;

            if (ValueListView.ContainerFromItem(valueItem) is not ListViewItem container)
                return;

            var anchor = UIHelper.FindVisualChildByName<Button>(container, "AiAnchor");

            if (anchor?.Flyout is not Flyout flyout || flyout.Content == null)
                return;

            var textBlock = flyout.Content.FindVisualChildByName<TextBlock>("AiExplanationText");
            var progressBar = flyout.Content.FindVisualChildByName<ProgressBar>("AiLoadingBar");

            if (textBlock == null || progressBar == null)
                return;

            try
            {
                textBlock.Text = ResourceString.GetString("values_viewer_ai_analyzing");
                progressBar.Visibility = Visibility.Visible;
                flyout.ShowAt(container);

                // Note: Retaining English context for the AI prompt engine to ensure maximum accuracy
                string currentPath = ViewModel.SelectedKeyItem?.Path ?? "Unknown Path";
                string context = $"This registry value is located at: {currentPath}\n" +
                                 $"Value Name: {valueItem.DisplayName}\n" +
                                 $"Value Type: {valueItem.TypeString}\n" +
                                 $"Current Data: {valueItem.DisplayValue}";

                string explanation = await AiExplainerService.ExplainGenericItemAsync(
                    valueItem.DisplayName,
                    "Windows Registry Value",
                    context);

                DispatcherQueue.TryEnqueue(() =>
                {
                    textBlock.Text = explanation;
                    progressBar.Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    textBlock.Text = string.Format(ResourceString.GetString("values_viewer_ai_failed"), ex.Message);
                    progressBar.Visibility = Visibility.Collapsed;
                });
            }
        }
        #endregion

        #region Search Event Handlers
        private void CloseSearch()
        {
            ViewModel.IsSearchActive = false;
            ViewModel.IsSearchRunning = false;
            ViewModel.SearchResults.Clear();
            ViewModel.StatusBarMessage = ResourceString.GetString("values_viewer_status_ready");
        }

        private void CloseSearch_Click(object sender, RoutedEventArgs e)
        {
            CloseSearch();
        }

        private void SearchResultsListView_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is RegistrySearchResult clickedResult)
            {
                WeakReferenceMessenger.Default.Send(new RegistryNavigationMessage(clickedResult.RootHive, clickedResult.FullPath, clickedResult.Name));
                CloseSearch();
            }
        }
        #endregion

        #region Import / Export Logic
        private async void OnImportButtonClick(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".reg");

            IntPtr hwnd = Win32Helper.GetActiveWindow();
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                string command = $"REG IMPORT \"{file.Path}\"";
                await CommandExecutor.RunCommand(command, isPowerShell: false);
            }
        }

        private async void OnExportButtonClick(object sender, RoutedEventArgs e)
        {
            var key = ViewModel.SelectedKeyItem;
            if (key == null) return;

            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.FileTypeChoices.Add("Registration Entries", new System.Collections.Generic.List<string> { ".reg" });
            picker.SuggestedFileName = "ExportedKey";

            IntPtr hwnd = Win32Helper.GetActiveWindow();
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                string command = $"REG EXPORT \"{key.PathForCmd}\" \"{file.Path}\" /y";
                await CommandExecutor.RunCommand(command, isPowerShell: false);
            }
        }
        #endregion

        #region Session History & Undo
        private void ToggleHistoryPane_Click(object sender, RoutedEventArgs e)
        {
            HistorySplitView.IsPaneOpen = !HistorySplitView.IsPaneOpen;
        }

        private void UndoLastButton_Click(object sender, RoutedEventArgs e)
        {
            RegistryTransactionManager.UndoLast();
            RefreshViewModel();
        }

        private void SpecificUndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RegistryTransaction transaction)
            {
                RegistryTransactionManager.UndoTransaction(transaction);
                RefreshViewModel();
            }
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            RegistryTransactionManager.SessionHistory.Clear();
        }

        private void RefreshViewModel()
        {
            if (this.DataContext is ValuesViewerViewModel vm)
            {
                vm.RefreshCurrentKey();
            }
        }
        #endregion

        #region Context Menu & Modification Actions
        private void ValueItemGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var grid = (Grid)sender;
            if (grid.DataContext is ValueItem clickedItem)
            {
                ValueListView.SelectedItem = clickedItem;

                if (grid.ContextFlyout is MenuFlyout flyout)
                {
                    var aiMenuItem = flyout.Items.OfType<MenuFlyoutItem>().FirstOrDefault(i => i.Name == "MenuAskAiValue");
                    if (aiMenuItem != null)
                    {
                        aiMenuItem.IsEnabled = ViewModel.IsAiReady;
                    }
                }
            }
        }

        private async void OnValueModify_Click(object sender, RoutedEventArgs e)
        {
            var item = (ValueItem)ValueListView.SelectedItem;
            if (item == null) return;

            var dialog = new ValueEditingDialog
            {
                ViewModel = new() { ValueItem = item },
                ParentViewModel = this.ViewModel,
                XamlRoot = this.Content.XamlRoot,
            };

            _ = await dialog.ShowAsync();
        }

        private async void OnValueModifyBinary_Click(object sender, RoutedEventArgs e)
        {
            var item = (ValueItem)ValueListView.SelectedItem;
            if (item == null) return;

            var dialog = new ValueEditingDialog
            {
                ViewModel = new() { ValueItem = item },
                ParentViewModel = this.ViewModel,
                XamlRoot = this.Content.XamlRoot,
            };

            _ = await dialog.ShowAsync();
        }

        private async void OnValueRename_Click(object sender, RoutedEventArgs e)
        {
            var item = (ValueItem)ValueListView.SelectedItem;
            var key = ViewModel.SelectedKeyItem;
            if (item == null || key == null) return;

            var inputTextBox = new TextBox { Text = item.Name, Width = 350 };
            inputTextBox.SelectAll();

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("values_viewer_rename_title"),
                Content = inputTextBox,
                PrimaryButtonText = ResourceString.GetString("values_viewer_rename_ok"),
                CloseButtonText = ResourceString.GetString("values_viewer_dialog_cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var confirmation = await dialog.ShowAsync();

            string newName = inputTextBox.Text.Trim();
            if (confirmation != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(newName) || newName == item.Name)
            {
                return;
            }

            var result = RegOpenKeyEx(key.RootHive, key.Path, 0, REGSAM.KEY_QUERY_VALUE | REGSAM.KEY_SET_VALUE, out var hKey);
            if (result.Failed)
            {
                ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_error_open"), result.FormatMessage());
                return;
            }

            uint cbData = 0;
            result = RegQueryValueEx(hKey, item.Name, IntPtr.Zero, out var type, IntPtr.Zero, ref cbData);

            if (result.Succeeded || result == Win32Error.ERROR_MORE_DATA)
            {
                using var dataHandle = new Vanara.InteropServices.SafeHGlobalHandle((int)cbData);
                result = RegQueryValueEx(hKey, item.Name, IntPtr.Zero, out type, dataHandle, ref cbData);

                if (result.Succeeded)
                {
                    result = RegSetValueEx(hKey, newName, 0, type, dataHandle, cbData);

                    if (result.Succeeded)
                    {
                        RegDeleteValue(hKey, item.Name);
                        await ViewModel.EnumerateRegistryValuesAsync(key.RootHive, key.Path);
                        ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_renamed"), newName);
                    }
                }
            }

            hKey?.Dispose();

            if (result.Failed)
            {
                ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_rename_failed"), result.FormatMessage());
            }
        }

        private async void OnValueDelete_Click(object sender, RoutedEventArgs e)
        {
            var item = (ValueItem)ValueListView.SelectedItem;
            var key = ViewModel.SelectedKeyItem;
            if (item == null || key == null) return;

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("values_viewer_delete_title"),
                Content = string.Format(ResourceString.GetString("values_viewer_delete_content"), item.DisplayName),
                PrimaryButtonText = ResourceString.GetString("values_viewer_delete_yes"),
                CloseButtonText = ResourceString.GetString("values_viewer_delete_no"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            var confirmation = await dialog.ShowAsync();
            if (confirmation != ContentDialogResult.Primary) return;

            var result = RegOpenKeyEx(key.RootHive, key.Path, 0, REGSAM.KEY_SET_VALUE, out var hKey);
            if (result.Succeeded)
            {
                result = RegDeleteValue(hKey, item.Name);
                hKey?.Dispose();

                if (result.Succeeded)
                {
                    await ViewModel.EnumerateRegistryValuesAsync(key.RootHive, key.Path);
                    ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_deleted"), item.DisplayName);
                }
                else
                {
                    ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_delete_failed"), result.FormatMessage());
                }
            }
            else
            {
                ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_error_open"), result.FormatMessage());
            }
        }

        private Microsoft.Win32.RegistryKey? GetRootKeyFromName(string rootName)
        {
            return rootName switch
            {
                "HKEY_CLASSES_ROOT" => Microsoft.Win32.Registry.ClassesRoot,
                "HKEY_CURRENT_USER" => Microsoft.Win32.Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" => Microsoft.Win32.Registry.LocalMachine,
                "HKEY_USERS" => Microsoft.Win32.Registry.Users,
                "HKEY_CURRENT_CONFIG" => Microsoft.Win32.Registry.CurrentConfig,
                _ => null
            };
        }

        private Microsoft.Win32.RegistryKey? GetDotNetRegistryKey(Vanara.PInvoke.HKEY hkey) => GetRootKeyFromName(hkey.ToString() ?? "");
        #endregion

        #region New Item Creation Logic
        private async void OnNewPrimaryClick(SplitButton sender, SplitButtonClickEventArgs args)
        {
            await OpenAddingDialog(RegistryValueType.String);
        }

        private async void OnNewKeyClick(object sender, RoutedEventArgs e) => await OpenAddingDialog(RegistryValueType.Key);
        private async void OnNewStringValueClick(object sender, RoutedEventArgs e) => await OpenAddingDialog(RegistryValueType.String);
        private async void OnNewBinaryValueClick(object sender, RoutedEventArgs e) => await OpenAddingDialog(RegistryValueType.Binary);
        private async void OnNewDwordValueClick(object sender, RoutedEventArgs e) => await OpenAddingDialog(RegistryValueType.Dword);
        private async void OnNewQwordValueClick(object sender, RoutedEventArgs e) => await OpenAddingDialog(RegistryValueType.Qword);
        private async void OnNewMultiStringValueClick(object sender, RoutedEventArgs e) => await OpenAddingDialog(RegistryValueType.MultiString);
        private async void OnNewExpandStringValueClick(object sender, RoutedEventArgs e) => await OpenAddingDialog(RegistryValueType.ExpandString);

        private async Task OpenAddingDialog(RegistryValueType type)
        {
            var dialog = new ValueAddingDialog(type)
            {
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var chosenType = dialog.SelectedType;
                string typedName = dialog.InputName.Trim();

                CreateNewRegistryItem(chosenType, typedName);
            }
        }

        private async void CreateNewRegistryItem(RegistryValueType type, string name)
        {
            var key = ViewModel.SelectedKeyItem;
            if (key == null) return;

            if (string.IsNullOrWhiteSpace(name))
            {
                name = type == RegistryValueType.Key
                    ? ResourceString.GetString("values_viewer_new_key_default")
                    : ResourceString.GetString("values_viewer_new_value_default");

                int counter = 1;
                string baseName = name.Replace("#1", "#");
                while (type != RegistryValueType.Key && ViewModel.ValueItems.Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    counter++;
                    name = $"{baseName}{counter}";
                }
            }

            if (type == RegistryValueType.Key)
            {
                var result = RegOpenKeyEx(key.RootHive, key.Path, 0, REGSAM.KEY_CREATE_SUB_KEY, out var hKey);
                if (result.Failed)
                {
                    ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_error"), result.FormatMessage());
                    return;
                }

                result = RegCreateKeyEx(hKey, name, 0, null, 0, REGSAM.KEY_WRITE, null, out var hNewKey, out var disposition);

                hNewKey?.Dispose();
                hKey?.Dispose();

                if (result.Succeeded)
                    ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_created_key"), name);
                else
                    ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_create_failed"), result.FormatMessage());

                return;
            }

            var valueResult = RegOpenKeyEx(key.RootHive, key.Path, 0, REGSAM.KEY_SET_VALUE, out var hValKey);
            if (valueResult.Failed)
            {
                ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_error"), valueResult.FormatMessage());
                return;
            }

            Win32Error setResult = Win32Error.ERROR_SUCCESS;

            switch (type)
            {
                case RegistryValueType.String:
                case RegistryValueType.ExpandString:
                    byte[] strBytes = Encoding.Unicode.GetBytes("\0");
                    var strType = type == RegistryValueType.String ? REG_VALUE_TYPE.REG_SZ : REG_VALUE_TYPE.REG_EXPAND_SZ;
                    setResult = RegSetValueEx(hValKey, name, 0, strType, strBytes, (uint)strBytes.Length);
                    break;

                case RegistryValueType.MultiString:
                    byte[] multiStrBytes = Encoding.Unicode.GetBytes("\0\0");
                    setResult = RegSetValueEx(hValKey, name, 0, REG_VALUE_TYPE.REG_MULTI_SZ, multiStrBytes, (uint)multiStrBytes.Length);
                    break;

                case RegistryValueType.Dword:
                    byte[] dwordBytes = BitConverter.GetBytes(0u);
                    setResult = RegSetValueEx(hValKey, name, 0, REG_VALUE_TYPE.REG_DWORD, dwordBytes, (uint)dwordBytes.Length);
                    break;

                case RegistryValueType.Qword:
                    byte[] qwordBytes = BitConverter.GetBytes(0ul);
                    setResult = RegSetValueEx(hValKey, name, 0, REG_VALUE_TYPE.REG_QWORD, qwordBytes, (uint)qwordBytes.Length);
                    break;

                case RegistryValueType.Binary:
                    byte[] binBytes = Array.Empty<byte>();
                    setResult = RegSetValueEx(hValKey, name, 0, REG_VALUE_TYPE.REG_BINARY, binBytes, (uint)binBytes.Length);
                    break;
            }

            hValKey?.Dispose();

            if (setResult.Succeeded)
            {
                await ViewModel.EnumerateRegistryValuesAsync(key.RootHive, key.Path);
                ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_created_val"), type);
            }
            else
            {
                ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_val_failed"), setResult.FormatMessage());
            }
        }
        #endregion

        #region Address Bar Logic
        private void EditPathButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedKey = ViewModel.SelectedKeyItem;
            if (selectedKey == null) return;

            string fullPath = "Computer\\";

            if (!selectedKey.SelectedRootComputer && !selectedKey.RootHive.IsNull)
            {
                string? hiveName = selectedKey.RootHive switch
                {
                    var h when h == HKEY.HKEY_CLASSES_ROOT => "HKEY_CLASSES_ROOT",
                    var h when h == HKEY.HKEY_CURRENT_USER => "HKEY_CURRENT_USER",
                    var h when h == HKEY.HKEY_LOCAL_MACHINE => "HKEY_LOCAL_MACHINE",
                    var h when h == HKEY.HKEY_USERS => "HKEY_USERS",
                    var h when h == HKEY.HKEY_CURRENT_CONFIG => "HKEY_CURRENT_CONFIG",
                    _ => selectedKey.RootHive.ToString()
                };

                fullPath += $"{hiveName}";

                if (!string.IsNullOrEmpty(selectedKey.Path))
                {
                    fullPath += $"\\{selectedKey.Path}";
                }
            }

            PathBreadcrumbBar.Visibility = Visibility.Collapsed;
            PathAddressBox.Visibility = Visibility.Visible;

            PathAddressBox.Text = fullPath;
            PathAddressBox.Focus(FocusState.Programmatic);
            PathAddressBox.SelectAll();
        }

        private void PathAddressBoxFlyout_Opened(object sender, object e)
        {
            _isAddressBoxMenuOpen = true;
        }

        private void PathAddressBoxFlyout_Closed(object sender, object e)
        {
            _isAddressBoxMenuOpen = false;

            DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(50);

                if (FocusManager.GetFocusedElement(XamlRoot) as TextBox != PathAddressBox)
                {
                    PathAddressBox.Visibility = Visibility.Collapsed;
                    PathBreadcrumbBar.Visibility = Visibility.Visible;
                }
            });
        }

        private void PathAddressBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isAddressBoxMenuOpen) return;

            PathAddressBox.Visibility = Visibility.Collapsed;
            PathBreadcrumbBar.Visibility = Visibility.Visible;
        }

        private void PathAddressBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                string input = PathAddressBox.Text.Trim();

                PathAddressBox.Visibility = Visibility.Collapsed;
                PathBreadcrumbBar.Visibility = Visibility.Visible;

                ParseAndNavigate(input);
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                PathAddressBox.Visibility = Visibility.Collapsed;
                PathBreadcrumbBar.Visibility = Visibility.Visible;
            }
        }

        private void ParseAndNavigate(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return;

            if (rawPath.StartsWith("Computer\\", StringComparison.OrdinalIgnoreCase))
            {
                rawPath = rawPath.Substring(9);
            }
            else if (rawPath.Equals("Computer", StringComparison.OrdinalIgnoreCase))
            {
                WeakReferenceMessenger.Default.Send(
                    new RegistryNavigationMessage(default, "Computer"));
                return;
            }

            string[] parts = rawPath.Split('\\', 2);
            string rootString = parts[0].ToUpperInvariant();
            string subPath = parts.Length > 1 ? parts[1] : "";

            HKEY targetHive = rootString switch
            {
                "HKEY_CLASSES_ROOT" or "HKCR" => HKEY.HKEY_CLASSES_ROOT,
                "HKEY_CURRENT_USER" or "HKCU" => HKEY.HKEY_CURRENT_USER,
                "HKEY_LOCAL_MACHINE" or "HKLM" => HKEY.HKEY_LOCAL_MACHINE,
                "HKEY_USERS" or "HKU" => HKEY.HKEY_USERS,
                "HKEY_CURRENT_CONFIG" or "HKCC" => HKEY.HKEY_CURRENT_CONFIG,
                _ => default
            };

            if (!targetHive.IsNull)
            {
                WeakReferenceMessenger.Default.Send(
                    new RegistryNavigationMessage(targetHive, subPath));
            }
            else
            {
                ViewModel.StatusBarMessage = string.Format(ResourceString.GetString("values_viewer_status_invalid_path"), rootString);
            }
        }

        private void PathBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            if (args.Index == ViewModel.SelectedKeyPathItems.Count - 1) return;

            var pathParts = new System.Collections.Generic.List<string>();

            for (int i = 0; i <= args.Index; i++)
            {
                var item = ViewModel.SelectedKeyPathItems[i];
                pathParts.Add(item.PathItem ?? "");
            }

            string rawPath = string.Join("\\", pathParts);
            ParseAndNavigate(rawPath);
        }
        #endregion

        #region AI Provider Helpers
        private async Task<bool> CheckConnectionAsync(AiProvider provider)
        {
            try
            {
                string? url = provider switch
                {
                    AiProvider.Groq => "https://api.groq.com/openai/v1/models",
                    AiProvider.OpenRouter => "https://openrouter.ai/api/v1/models",
                    AiProvider.Mistral => "https://api.mistral.ai/v1/models",
                    AiProvider.Gemini => "https://generativelanguage.googleapis.com/v1beta/models",
                    AiProvider.Cohere => "https://api.cohere.com/v1/models",
                    _ => null
                };

                if (url == null) return false;

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                string key = GetKeyForProvider(provider);

                if (!string.IsNullOrEmpty(key))
                {
                    if (provider == AiProvider.Gemini)
                    {
                        request.Headers.Add("x-goog-api-key", key);
                    }
                    else
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    }
                }

                var response = await _http.SendAsync(request);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private string GetKeyForProvider(AiProvider provider) => provider switch
        {
            AiProvider.Groq => LocalMachineSettingsEngine.GroqApiKey,
            AiProvider.Gemini => LocalMachineSettingsEngine.GeminiApiKey,
            AiProvider.OpenRouter => LocalMachineSettingsEngine.OpenRouterApiKey,
            AiProvider.Cohere => LocalMachineSettingsEngine.CohereApiKey,
            AiProvider.Mistral => LocalMachineSettingsEngine.MistralApiKey,
            _ => ""
        };
        #endregion
    }
}