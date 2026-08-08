// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Security;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class PasswordManagerPage : Page, IPurgeable
    {
        #region Fields & Initialization

        private PasswordManagerViewModel? ViewModel;
        private Views.PasswordGeneratorWindow? _generatorWindow;

        public PasswordManagerPage()
        {
            this.InitializeComponent();
        }

        #endregion

        #region Navigation & Lifecycle

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is (string username, SecureString masterSecurePassword))
            {
                ViewModel = new PasswordManagerViewModel(username, masterSecurePassword);
                DataContext = ViewModel;

                ViewModel.RequestOpenRecordModal = OpenRecordModal;

                ViewModel.AddRecordVM.CloseRequestedAction = () =>
                {
                    RecordPasswordBox.Password = string.Empty;
                    CloseSidePanel();
                };

                if (ViewModel.LoadDataCommand.CanExecute(null))
                {
                    ViewModel.LoadDataCommand.Execute(null);
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                _ = Purge();
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null && this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) { }

        #endregion

        #region Record Management (Add, Edit, Info)

        private void BtnAddRecord_Click(object sender, RoutedEventArgs e)
        {
            OpenRecordModal(null);
        }

        private void OpenRecordModal(PasswordEntry? entryToEdit)
        {
            if (ViewModel == null) return;

            ViewModel.AddRecordVM.Initialize(entryToEdit);

            var popup = AddRecordPopup ?? (Popup)this.FindName("AddRecordPopup");
            if (popup != null)
            {
                popup.IsOpen = true;

                UIHelper.SetOverlay(true);

                MainContentView.IsHitTestVisible = false;
            }
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e) { }

        #endregion

        #region Modal Interaction (Save & Close)

        private void BtnCloseModal_Click(object sender, RoutedEventArgs e)
        {
            CloseSidePanel();
        }

        private void BtnSaveModal_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.AddRecordVM != null)
            {
                ViewModel.AddRecordVM.RecordPassword = RecordPasswordBox.Password;

                if (ViewModel.AddRecordVM.SaveCommand.CanExecute(null))
                {
                    ViewModel.AddRecordVM.SaveCommand.Execute(null);
                }
                else
                {
                    ViewModel.AddRecordVM.StatusMessage = "Please fill in the required Title and Password.";
                }
            }
        }

        private void CloseSidePanel()
        {
            var popup = AddRecordPopup ?? (Popup)this.FindName("AddRecordPopup");
            if (popup != null)
            {
                popup.IsOpen = false;

                UIHelper.SetOverlay(false);
                MainContentView.IsHitTestVisible = true;
            }
        }

        private void RecordPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is PasswordManagerViewModel mainVm)
            {
                mainVm.AddRecordVM.RecordPassword = RecordPasswordBox.Password;
            }
            else if (this.DataContext is AddRecordViewModel addVm)
            {
                addVm.RecordPassword = RecordPasswordBox.Password;
            }
        }

        #endregion

        #region Tools & Settings

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var popup = PopupSettings ?? (Popup)this.FindName("PopupSettings");
            if (popup != null)
            {
                popup.IsOpen = !popup.IsOpen;
            }
        }

        private void BtnPasswordGenerator_Click(object sender, RoutedEventArgs e)
        {
            if (_generatorWindow == null)
            {
                _generatorWindow = new Views.PasswordGeneratorWindow();
                _generatorWindow.Closed += (s, args) => { _generatorWindow = null; };
            }
            _generatorWindow.Activate();
        }

        #endregion

        #region Purge Page
        public Task Purge()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

            if (_generatorWindow != null)
            {
                _generatorWindow.Close();
                _generatorWindow = null;
            }

            if (RecordPasswordBox != null)
            {
                RecordPasswordBox.Password = string.Empty;
            }

            var addPopup = AddRecordPopup ?? (Popup)this.FindName("AddRecordPopup");
            if (addPopup != null) addPopup.IsOpen = false;

            var settingsPopup = PopupSettings ?? (Popup)this.FindName("PopupSettings");
            if (settingsPopup != null) settingsPopup.IsOpen = false;

            UIHelper.SetOverlay(false);

            if (MainContentView != null) MainContentView.IsHitTestVisible = true;

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking Passwords & UI...");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(350);

                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (ViewModel != null)
                        {
                            if (ViewModel.AddRecordVM != null)
                            {
                                ViewModel.AddRecordVM.CloseRequestedAction = null;
                            }
                        }

                        ViewModel = null;

                        this.Bindings?.StopTracking();
                        this.DataContext = null;
                        this.Content = null;
                    });

                    DiagnosticsPageViewModel.Current?.ForceImmediateMemoryCleanup();
                });
            }
            else
            {
                Debug.WriteLine($"[{this.GetType().Name}] High Performance Mode: State preserved in RAM cache.");
            }

            return Task.CompletedTask;
        }
        #endregion
    }
}