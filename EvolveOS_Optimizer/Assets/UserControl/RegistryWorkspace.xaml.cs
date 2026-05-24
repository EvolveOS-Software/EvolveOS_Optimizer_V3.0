// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Helpers;
using EvolveOS_Optimizer.Pages;

namespace EvolveOS_Optimizer.Assets.UserControl
{
    public sealed partial class RegistryWorkspace : Microsoft.UI.Xaml.Controls.UserControl
    {
        #region Properties
        public RegistryEditorViewModel ViewModel { get; private set; }

        public ValuesViewerViewModel ValuesViewerViewModel { get; } = new ValuesViewerViewModel();
        #endregion

        #region Constructor
        public RegistryWorkspace()
        {
            this.InitializeComponent();

            this.ViewModel = new RegistryEditorViewModel();

            CustomMainTreeView.ValuesViewerViewModel = this.ValuesViewerViewModel;

            ContentFrame.Navigate(typeof(ValuesViewerPage), this.ValuesViewerViewModel);

            this.Loaded += RegistryWorkspace_Loaded;
            this.Unloaded += RegistryWorkspace_Unloaded;
        }
        #endregion

        #region Lifecycle Events
        private void RegistryWorkspace_Loaded(object sender, RoutedEventArgs e)
        {
            ValuesViewerViewModel.PropertyChanged += ValuesViewerViewModel_PropertyChanged;
        }

        private void RegistryWorkspace_Unloaded(object sender, RoutedEventArgs e)
        {
            ValuesViewerViewModel.PropertyChanged -= ValuesViewerViewModel_PropertyChanged;
        }

        private void ValuesViewerViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ValuesViewerViewModel.IsSearchActive))
            {
                DispatcherQueue.TryEnqueue(() => UpdateContentVisibility());
            }
        }
        #endregion

        #region TreeView Event Handlers
        private void CustomMainTreeView_BaseSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = CustomMainTreeView.GetSelectedItem();

            if (selectedItem != null && ValuesViewerViewModel.SelectedKeyItem != selectedItem)
            {
                ValuesViewerViewModel.SelectedKeyItem = selectedItem;
                UpdateContentVisibility();
            }
        }

        private void UpdateContentVisibility()
        {
            bool isSearchActive = ValuesViewerViewModel.IsSearchActive;
            var selected = ValuesViewerViewModel.SelectedKeyItem;

            ContentFrame.Visibility = Visibility.Visible;

            if (!isSearchActive && (selected == null || selected.SelectedRootComputer || selected.RootHive.IsNull))
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyStatePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CustomMainTreeView_KeyDeleting(object sender, RoutedEventArgs e) { }
        private void CustomMainTreeView_KeyRenaming(object sender, RoutedEventArgs e) { }

        private async void CustomMainTreeView_KeyExporting(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            await ViewModel.ExportSelectedKeyTree(CustomMainTreeView.GetSelectedItem());
        }

        private void CustomMainTreeView_KeyPropertyWindowOpening(object sender, RoutedEventArgs e)
        {
            PropertyWindowHelpers.CreatePropertyWindow(CustomMainTreeView.GetSelectedItem());
        }
        #endregion
    }
}