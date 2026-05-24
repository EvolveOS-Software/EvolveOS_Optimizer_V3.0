// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Vanara.PInvoke;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class ValueItem : ObservableObject
    {
        #region Constructor
        public ValueItem()
        {
            Type = REG_VALUE_TYPE.REG_NONE;
            IsRenamable = true;
            DataIsEditable = true;
        }
        #endregion

        #region Name & Display Properties
        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string _displayName = string.Empty;
        public string DisplayName { get => _displayName; set => SetProperty(ref _displayName, value); }
        #endregion

        #region Value Data Properties
        private string _displayValue = string.Empty;
        public string DisplayValue { get => _displayValue; set => SetProperty(ref _displayValue, value); }

        private string _editableValue = string.Empty;
        public string EditableValue { get => _editableValue; set => SetProperty(ref _editableValue, value); }

        private uint _dataSize;
        public uint DataSize { get => _dataSize; set => SetProperty(ref _dataSize, value); }
        #endregion

        #region Type Metadata
        private REG_VALUE_TYPE _type;
        public REG_VALUE_TYPE Type { get => _type; set => SetProperty(ref _type, value); }

        private string _typeString = string.Empty;
        public string TypeString { get => _typeString; set => SetProperty(ref _typeString, value); }
        #endregion

        #region Editing Permissions
        private bool _isRenamable;
        public bool IsRenamable { get => _isRenamable; set => SetProperty(ref _isRenamable, value); }

        private bool _dataIsEditable;
        public bool DataIsEditable { get => _dataIsEditable; set => SetProperty(ref _dataIsEditable, value); }
        #endregion
    }
}