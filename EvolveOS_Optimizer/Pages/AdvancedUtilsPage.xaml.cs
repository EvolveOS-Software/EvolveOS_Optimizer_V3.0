// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class AdvancedUtilsPage : Page
    {
        public AdvancedUtilsViewModel ViewModel { get; }

        public AdvancedUtilsPage()
        {
            this.InitializeComponent();
            ViewModel = new AdvancedUtilsViewModel();
            this.DataContext = ViewModel;
        }
    }
}