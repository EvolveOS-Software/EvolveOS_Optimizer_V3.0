// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.ViewModel;
using Vanara.PInvoke;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class ValueEditingDialog : ContentDialog
    {
        #region propdp
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(ValueEditingDialogViewModel),
                typeof(ValueEditingDialog),
                new PropertyMetadata(null));

        public ValueEditingDialogViewModel ViewModel
        {
            get => (ValueEditingDialogViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public ValuesViewerViewModel ParentViewModel { get; set; } = null!;
        #endregion

        public ValueEditingDialog()
        {
            InitializeComponent();
        }

        private void OnValueEditorTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel?.ValueItem != null && sender is TextBox textBox)
            {
                ViewModel.ValueItem.EditableValue = textBox.Text;
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var parentKey = ParentViewModel.SelectedKeyItem;

            if (parentKey == null || ViewModel?.ValueItem == null)
                return;

            var result = ParentViewModel.SaveRegistryValue(parentKey, ViewModel.ValueItem);

            if (result.Failed)
            {
                args.Cancel = true;
                this.Title = $"Error: {result.FormatMessage()}";
            }
            else
            {
                await ParentViewModel.EnumerateRegistryValuesAsync(parentKey.RootHive, parentKey.Path);
            }
        }
    }
}