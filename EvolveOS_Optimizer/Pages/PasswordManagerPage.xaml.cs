// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Security;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class PasswordManagerPage : Page
    {
        private PasswordManagerViewModel? ViewModel;
        private Views.PasswordGeneratorWindow? _generatorWindow;

        public PasswordManagerPage()
        {
            this.InitializeComponent();
        }

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

        private void Page_Loaded(object sender, RoutedEventArgs e) { }
        private void InfoButton_Click(object sender, RoutedEventArgs e) { }
    }
}