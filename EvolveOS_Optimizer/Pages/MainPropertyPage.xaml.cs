// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;
using Microsoft.UI.Windowing;
using Windows.Foundation.Metadata;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class MainPropertyPage : Page
    {
        #region Fields
        public AppWindow? AppWindow;
        public KeyItem? KeyItem;
        #endregion

        #region Constructor
        public MainPropertyPage()
        {
            InitializeComponent();
        }
        #endregion

        #region Navigation
        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            switch (args.SelectedItemContainer.Tag)
            {
                case "General":
                    contentFrame.Navigate(typeof(GeneralPage), KeyItem, args.RecommendedNavigationTransitionInfo);
                    break;

                case "Security":
                    contentFrame.Navigate(typeof(SecurityPage), KeyItem, args.RecommendedNavigationTransitionInfo);
                    break;
            }
        }
        #endregion

        #region Action Handlers
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            bool isSaveSuccessful = true;

            if (contentFrame.Content is SecurityPage securityPage)
            {
                isSaveSuccessful = securityPage.SaveProperties();
            }
            else if (contentFrame.Content is GeneralPage generalPage)
            {
                isSaveSuccessful = generalPage.SaveProperties();
            }

            if (isSaveSuccessful)
            {
                CloseCurrentWindow();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CloseCurrentWindow();
        }
        #endregion

        #region Window Management
        private void CloseCurrentWindow()
        {
            if (contentFrame != null) contentFrame.Content = null;
            KeyItem = null;

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                if (AppWindow != null)
                {
                    AppWindow.Destroy();
                    return;
                }

                if (this.XamlRoot != null && this.XamlRoot.ContentIslandEnvironment != null)
                {
                    var windowId = this.XamlRoot.ContentIslandEnvironment.AppWindowId;
                    var currentAppWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    currentAppWindow?.Destroy();
                }
            }
        }
        #endregion
    }
}
