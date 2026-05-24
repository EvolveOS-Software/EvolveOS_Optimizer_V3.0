// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Xaml.Media.Animation;
using Vanara.PInvoke;
using Windows.Graphics;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SecurityPage : Page
    {
        #region Fields
        private static readonly System.Collections.Generic.List<Window> _activeAdvancedWindows = new();
        public SecurityViewModel ViewModel { get; } = new SecurityViewModel();
        #endregion

        #region Constructor
        public SecurityPage()
        {
            InitializeComponent();
        }
        #endregion

        #region Navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is KeyItem keyItem)
            {
                ViewModel.KeyItem = keyItem;
                ViewModel.GetKeyAccessControlList();
            }
            else
            {
                Debug.WriteLine("Navigation parameter was not a valid KeyItem.");
            }
        }
        #endregion

        #region State Management
        public bool SaveProperties()
        {
            if (!ViewModel.HasDacl || ViewModel.KeyItem == null)
                return true;

            var result = ViewModel.SaveKeySecurity();

            if (result.Failed)
            {
                Debug.WriteLine($"Failed to save permissions: {result.FormatMessage()}");

                return false;
            }

            return true;
        }
        #endregion

        #region UI Event Handlers
        private void ViewAdvancedSecurityButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var newFrame = new Frame();
            newFrame.Navigate(typeof(SecurityAdvancedPage), ViewModel.KeyItem, new SuppressNavigationTransitionInfo());

            var propertiesWindow = new Window()
            {
                Content = newFrame
            };

            UIHelper.ApplyBackdrop(propertiesWindow, SettingsEngine.Backdrop);

            _activeAdvancedWindows.Add(propertiesWindow);
            propertiesWindow.Closed += (s, args) =>
            {
                _activeAdvancedWindows.Remove(propertiesWindow);
            };

            var appWindow = propertiesWindow.AppWindow;

            if (newFrame.Content is SecurityAdvancedPage properties)
                properties.AppWindow = appWindow;

            appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "EvolveOS_Optimizer.ico"));
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            appWindow.Title = "Advanced Permissions";
            appWindow.Resize(new SizeInt32(850, 550));

            appWindow.Show();
        }

        private void MergedPermissionPrincipalsListView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listView = (ListView)sender;

            if (listView.ItemsSource != null && ViewModel.Principals.Count != 0)
            {
                listView.SelectedIndex = 0;
            }
        }
        #endregion
    }
}
