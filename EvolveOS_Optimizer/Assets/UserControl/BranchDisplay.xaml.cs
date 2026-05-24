// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;

namespace EvolveOS_Optimizer.Assets.UserControl
{
    public sealed partial class BranchDisplay : Microsoft.UI.Xaml.Controls.UserControl
    {
        #region Dependency Properties
        public static readonly DependencyProperty NumberOfBranchProperty =
            DependencyProperty.Register(nameof(NumberOfBranch), typeof(int), typeof(BranchDisplay), new PropertyMetadata(0, OnNumberOfBranchChanged));

        public int NumberOfBranch
        {
            get => (int)GetValue(NumberOfBranchProperty);
            set => SetValue(NumberOfBranchProperty, value);
        }

        public static readonly DependencyProperty HasChildrenProperty =
            DependencyProperty.Register(nameof(HasChildren), typeof(bool), typeof(BranchDisplay), new PropertyMetadata(false));

        public bool HasChildren
        {
            get => (bool)GetValue(HasChildrenProperty);
            set => SetValue(HasChildrenProperty, value);
        }
        #endregion

        #region Fields & Properties
        private readonly ObservableCollection<bool> _branches;
        public ReadOnlyObservableCollection<bool> Branches { get; }
        #endregion

        #region Constructor
        public BranchDisplay()
        {
            this.InitializeComponent();

            _branches = new ObservableCollection<bool>();
            Branches = new ReadOnlyObservableCollection<bool>(_branches);
        }
        #endregion

        #region Logic
        private static void OnNumberOfBranchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BranchDisplay control)
            {
                control.UpdateBranches();
            }
        }

        private void UpdateBranches()
        {
            _branches.Clear();
            for (int i = 0; i < NumberOfBranch - 1; i++)
            {
                _branches.Add(true);
            }
        }
        #endregion
    }
}