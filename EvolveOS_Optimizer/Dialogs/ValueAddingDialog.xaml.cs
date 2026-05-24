// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Dialogs
{
    #region Registry Types
    public enum RegistryValueType
    {
        Key,
        String,
        Binary,
        Dword,
        Qword,
        MultiString,
        ExpandString
    }
    #endregion

    public sealed partial class ValueAddingDialog : ContentDialog
    {
        #region Properties
        public RegistryValueType SelectedType { get; set; }
        public string InputName => NameInput.Text;
        #endregion

        #region Constructor
        public ValueAddingDialog(RegistryValueType initialType = RegistryValueType.String)
        {
            InitializeComponent();

            SelectedType = initialType;
            TypeComboBox.SelectedIndex = (int)initialType;
        }
        #endregion
    }
}