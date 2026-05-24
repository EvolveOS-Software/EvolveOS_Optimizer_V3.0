// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class GeneralPage : Page
    {
        #region Properties
        public GeneralViewModel ViewModel { get; }
        #endregion

        #region Constructor
        public GeneralPage()
        {
            InitializeComponent();

            this.ViewModel = new GeneralViewModel(this.DispatcherQueue);
        }
        #endregion

        #region Navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is KeyItem keyItem)
            {
                ViewModel.KeyItem = keyItem;
                this.Bindings.Update();
            }
        }
        #endregion

        #region State Management
        public bool SaveProperties()
        {
            return true;
        }
        #endregion
    }
}
