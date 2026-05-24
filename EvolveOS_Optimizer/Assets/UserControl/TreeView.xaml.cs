// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Vanara.PInvoke;

namespace EvolveOS_Optimizer.Assets.UserControl
{
    public sealed partial class TreeView : Microsoft.UI.Xaml.Controls.UserControl
    {
        #region Constructor
        public TreeView()
        {
            InitializeComponent();

            ViewModel = new TreeViewViewModel();
            WeakReferenceMessenger.Default.Register<RegistryNavigationMessage>(this, OnRegistryNavigationRequested);
        }
        #endregion

        #region Fields and Properties
        public TreeViewViewModel ViewModel { get; }
        public ValuesViewerViewModel ValuesViewerViewModel { get; set; } = null!;

        public event SelectionChangedEventHandler BaseSelectionChanged = delegate { };
        public event RoutedEventHandler KeyDeleting = delegate { };
        public event RoutedEventHandler KeyExporting = delegate { };
        public event RoutedEventHandler KeyRenaming = delegate { };
        public event RoutedEventHandler KeyPropertyWindowOpening = delegate { };
        #endregion

        #region TreeView event methods
        private void CustomMainTreeView_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => BaseSelectionChanged?.Invoke(sender, e);

        private async void ExpandCollapseButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (KeyItem)((Button)sender).DataContext;

            if (!item.IsExpanded)
            {
                await ViewModel.ExpandChildrenAsync(item);
            }
            else
            {
                ViewModel.CollapseChildren(item);
            }
        }
        #endregion

        #region MenuFlyout event methods
        private void KeyTreeViewItemMenuFlyout_Opening(object sender, object e)
        {
            var flyout = (MenuFlyout)sender;
            var target = (Grid)flyout.Target;
            var item = (KeyItem)target.DataContext;

            if (item.SelectedRootComputer && flyout.Items.Count > 5)
            {
                flyout.Items.RemoveAt(1);
                flyout.Items.RemoveAt(1);
                flyout.Items.RemoveAt(1);
                flyout.Items.RemoveAt(1);
                flyout.Items.RemoveAt(1);
                flyout.Items.RemoveAt(3);
                flyout.Items.RemoveAt(3);
                flyout.Items.RemoveAt(3);
                return;
            }
        }

        private void KeyTreeViewItemMenuFlyout_Opened(object sender, object e)
        {
            var flyout = (MenuFlyout)sender;
            var target = (Grid)flyout.Target;
            var item = (KeyItem)target.DataContext;

            SelectItem(item);

            var menuAskAi = flyout.Items.OfType<MenuFlyoutItem>().FirstOrDefault(i => i.Name == "MenuAskAi");

            if (menuAskAi != null)
            {
                menuAskAi.IsEnabled = AiExplainerService.IsAiReady;
            }
        }

        private async void KeyTreeViewItemMenuFlyoutExpand_Click(object sender, RoutedEventArgs e)
        {
            var item = (KeyItem)CustomMainTreeView.SelectedItem;

            if (item != null && !item.IsExpanded)
            {
                await ViewModel.ExpandChildrenAsync(item);
            }
        }

        private void KeyTreeViewItemMenuFlyoutCollapse_Click(object sender, RoutedEventArgs e)
        {
            var item = (KeyItem)CustomMainTreeView.SelectedItem;

            if (item != null)
            {
                ViewModel.CollapseChildren(item);
            }
        }

        private async void KeyTreeViewItemMenuFlyoutNew_Click(object sender, RoutedEventArgs e)
        {
            var item = (KeyItem)CustomMainTreeView.SelectedItem;

            if (!item.IsExpanded && item.HasChildren)
            {
                await ViewModel.ExpandChildrenAsync(item);
            }

            var itemIndex = CustomMainTreeView.SelectedIndex + 1;

            item.HasChildren = true;

            string keyName = ResourceString.GetString("treeview_new_key_default");

            var defaultNewKeyItem = new KeyItem()
            {
                Name = keyName,
                RootHive = item.RootHive,
                BasePath = item.Path,
                IsDeletable = true,
                IsRenamable = true,
                IsRenaming = true,
                HasChildren = false,
                Image = "ms-appx:///Assets/PngImages/Folder.png",
                Depth = item.Depth + 1,
                Parent = item,
            };

            ViewModel.CreatingNewKey = true;

            ViewModel.FlatKeyItems.Insert(itemIndex, defaultNewKeyItem);

            item.Children.Insert(0, defaultNewKeyItem);

            CustomMainTreeView.SelectedIndex = itemIndex;
        }

        private async void KeyTreeViewItemMenuFlyoutDelete_Click(object sender, RoutedEventArgs e)
        {
            var item = (KeyItem)CustomMainTreeView.SelectedItem;
            if (item == null) return;

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("treeview_delete_title"),
                Content = string.Format(ResourceString.GetString("treeview_delete_content"), item.Name),
                PrimaryButtonText = ResourceString.GetString("treeview_delete_yes"),
                CloseButtonText = ResourceString.GetString("treeview_delete_no"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var confirmation = await dialog.ShowAsync();

            if (confirmation != ContentDialogResult.Primary)
            {
                return;
            }

            var result = ViewModel.DeleteRegistryKey(item);

            if (result.Succeeded)
            {
                RemoveItemRecursively(item);

                KeyDeleting?.Invoke(sender, e);
                ValuesViewerViewModel.StatusBarMessage = string.Format(ResourceString.GetString("treeview_status_deleted"), item.Name);
            }
            else
            {
                ValuesViewerViewModel.StatusBarMessage = string.Format(ResourceString.GetString("treeview_status_delete_failed"), result.FormatMessage());
            }
        }

        private void KeyTreeViewItemMenuFlyoutRename_Click(object sender, RoutedEventArgs e)
            => ((KeyItem)CustomMainTreeView.SelectedItem).IsRenaming = true;

        private void KeyTreeViewItemMenuFlyoutExport_Click(object sender, RoutedEventArgs e)
            => KeyExporting?.Invoke(sender, e);

        private void KeyTreeViewItemMenuFlyoutPermissions_Click(object sender, RoutedEventArgs e)
            => KeyPropertyWindowOpening?.Invoke(sender, e);

        private void KeyTreeViewItemMenuFlyoutCopyKeyName_Click(object sender, RoutedEventArgs e)
        {
            var item = (KeyItem)CustomMainTreeView.SelectedItem;

            if (item != null)
            {
                ClipBoardHelpers.SetContent(item.PathForPwsh);
            }
        }

        private async void KeyTreeViewItemMenuFlyoutAskAi_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem { DataContext: KeyItem item })
                return;

            if (CustomMainTreeView.ContainerFromItem(item) is not ListViewItem container)
                return;

            var anchor = container.FindVisualChildByName<Button>("AiAnchor");
            if (anchor?.Flyout is not Flyout flyout || flyout.Content == null)
                return;

            var textBlock = flyout.Content.FindVisualChildByName<TextBlock>("AiExplanationText");
            var progressBar = flyout.Content.FindVisualChildByName<ProgressBar>("AiLoadingBar");

            if (textBlock == null || progressBar == null)
                return;

            try
            {
                textBlock.Text = ResourceString.GetString("treeview_ai_analyzing");
                progressBar.Visibility = Visibility.Visible;
                flyout.ShowAt(container);

                string context = $"This is a Windows Registry Key.\n" +
                                 $"Key Name: {item.Name}\n" +
                                 $"Full Path: {item.Path}";

                string explanation = await AiExplainerService.ExplainGenericItemAsync(
                    item.Name,
                    "Windows Registry Key",
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
                    textBlock.Text = string.Format(ResourceString.GetString("treeview_ai_failed"), ex.Message);
                    progressBar.Visibility = Visibility.Collapsed;
                });
            }
        }

        // Analysis Dialog
        /*private async void KeyTreeViewItemMenuFlyoutAskAi_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is KeyItem item)
            {
                var provider = LocalMachineSettingsEngine.ActiveAiProvider;
                string? apiKey = provider switch
                {
                    Enums.AiProvider.Groq => LocalMachineSettingsEngine.GroqApiKey,
                    Enums.AiProvider.Gemini => LocalMachineSettingsEngine.GeminiApiKey,
                    Enums.AiProvider.OpenRouter => LocalMachineSettingsEngine.OpenRouterApiKey,
                    Enums.AiProvider.Cohere => LocalMachineSettingsEngine.CohereApiKey,
                    Enums.AiProvider.Mistral => LocalMachineSettingsEngine.MistralApiKey,
                    _ => null
                };

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Debug.WriteLine("Ask AI aborted: No valid API key found for active provider.");
                    return;
                }

                var dialog = new AiAnalysisDialog();

                dialog.XamlRoot = XamlRoot ?? this.Content?.XamlRoot;

                if (dialog.XamlRoot == null)
                {
                    Debug.WriteLine("CRITICAL: XamlRoot is null. Cannot show AI analysis dialog.");
                    return;
                }

                await dialog.ShowAndAnalyzeAsync(item.Name, "Registry Key", $"Registry Path: {item.Path}");
            }
        }*/
        #endregion

        #region TextBox event for renaming
        private void KeyItemNameRenamingTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            textBox.Focus(FocusState.Programmatic);
            textBox.SelectAll();
        }

        private void KeyItemNameRenamingTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            CommitNameChange(textBox);
        }

        private void KeyItemNameRenamingTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                var textBox = (TextBox)sender;
                CommitNameChange(textBox);
            }
        }

        private void CommitNameChange(TextBox textBox)
        {
            var item = (KeyItem)CustomMainTreeView.SelectedItem;
            if (item == null) return;

            string newName = textBox.Text;

            if (item.Name == newName && !ViewModel.CreatingNewKey)
            {
                item.IsRenaming = false;
                return;
            }

            if (ViewModel.CreatingNewKey)
            {
                ViewModel.CreatingNewKey = false;
                ViewModel.LastRenamedNewName = newName;
                item.Name = newName;
                KeyRenaming?.Invoke(newName, new RoutedEventArgs());
            }
            else
            {
                var result = ViewModel.RenameRegistryKey(item, newName);

                if (result.Succeeded)
                {
                    item.Name = newName;
                    ViewModel.LastRenamedNewName = newName;
                    ValuesViewerViewModel.StatusBarMessage = string.Format(ResourceString.GetString("treeview_status_renamed"), newName);

                    KeyRenaming?.Invoke(newName, new RoutedEventArgs());
                }
                else
                {
                    ValuesViewerViewModel.StatusBarMessage = string.Format(ResourceString.GetString("treeview_status_rename_failed"), result.FormatMessage());
                }
            }

            item.IsRenaming = false;
        }
        #endregion

        #region Search Navigation Logic
        private void OnRegistryNavigationRequested(object recipient, RegistryNavigationMessage message)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                var computerNode = ViewModel.FlatKeyItems.FirstOrDefault(k => k.SelectedRootComputer || k.Depth == 0);
                if (computerNode != null && !computerNode.IsExpanded)
                {
                    await ViewModel.ExpandChildrenAsync(computerNode);
                    computerNode.IsExpanded = true;
                    await Task.Delay(20);
                }

                var currentItem = ViewModel.FlatKeyItems.FirstOrDefault(k => k.RootHive == message.RootHive && (k.Depth == 1 || string.IsNullOrEmpty(k.Path)));

                if (currentItem == null)
                {
                    ValuesViewerViewModel.StatusBarMessage = ResourceString.GetString("treeview_nav_root_not_found");
                    return;
                }

                string pathWithoutRoot = message.Path ?? "";
                if (pathWithoutRoot.StartsWith(message.RootHive.ToString() ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    int firstSlash = pathWithoutRoot.IndexOf('\\');
                    if (firstSlash > -1) pathWithoutRoot = pathWithoutRoot.Substring(firstSlash + 1);
                }

                string[] folders = pathWithoutRoot.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                string currentCumulativePath = "";

                foreach (var folder in folders)
                {
                    currentCumulativePath = string.IsNullOrEmpty(currentCumulativePath)
                        ? folder
                        : $"{currentCumulativePath}\\{folder}";

                    if (!currentItem.IsExpanded)
                    {
                        await ViewModel.ExpandChildrenAsync(currentItem);
                        currentItem.IsExpanded = true;

                        await Task.Delay(50);
                    }

                    var nextItem = currentItem.Children?.FirstOrDefault(c => c.Name.Equals(folder, StringComparison.OrdinalIgnoreCase));

                    if (nextItem == null)
                    {
                        nextItem = ViewModel.FlatKeyItems.FirstOrDefault(k =>
                            k.RootHive == message.RootHive &&
                            k.Path != null &&
                            k.Path.Equals(currentCumulativePath, StringComparison.OrdinalIgnoreCase));
                    }

                    if (nextItem == null)
                    {
                        ValuesViewerViewModel.StatusBarMessage = string.Format(ResourceString.GetString("treeview_nav_folder_not_found"), folder);
                        break;
                    }

                    currentItem = nextItem;
                }

                SelectItem(currentItem);

                CustomMainTreeView.ScrollIntoView(currentItem);

                await Task.Delay(20);

                if (CustomMainTreeView.ContainerFromItem(currentItem) is Control container)
                {
                    container.Focus(FocusState.Programmatic);
                }

                if (!string.IsNullOrEmpty(message.TargetValueName))
                {
                    await Task.Delay(50);

                    var targetValue = ValuesViewerViewModel.ValueItems.FirstOrDefault(v => v.Name.Equals(message.TargetValueName, StringComparison.OrdinalIgnoreCase));
                    if (targetValue != null)
                    {
                        ValuesViewerViewModel.SelectedValueItem = targetValue;
                    }
                }
            });
        }
        #endregion

        #region Public methods for MainPage
        public void UnselectItem()
        {
            CustomMainTreeView.SelectedIndex = -1;
        }

        public void SelectItem(KeyItem item)
        {
            int index = ((ObservableCollection<KeyItem>)CustomMainTreeView.ItemsSource).IndexOf(item);
            CustomMainTreeView.SelectedIndex = index;
        }

        public KeyItem GetSelectedItem()
        {
            return (KeyItem)CustomMainTreeView.SelectedItem;
        }

        public void RemoveItem(KeyItem item)
        {
            if (item == null) return;

            ViewModel.FlatKeyItems.Remove(item);

            if (item.Parent != null && item.Parent.Children != null)
            {
                item.Parent.Children.Remove(item);
            }
        }

        public void RemoveItemRecursively(KeyItem item)
        {
            if (item == null) return;

            int startIndex = ViewModel.FlatKeyItems.IndexOf(item);
            if (startIndex == -1) return;

            int depth = item.Depth;

            var list = ViewModel.FlatKeyItems.Where(x => x.Depth > depth && ViewModel.FlatKeyItems.IndexOf(x) > startIndex).ToList();

            if (list.Count != 0)
            {
                var lastRemovedItemIndex = ViewModel.FlatKeyItems.IndexOf(list.First());

                foreach (var listItem in list)
                {
                    if (lastRemovedItemIndex == ViewModel.FlatKeyItems.IndexOf(listItem))
                    {
                        ViewModel.FlatKeyItems.Remove(listItem);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            RemoveItem(item);
        }
        #endregion
    }
}