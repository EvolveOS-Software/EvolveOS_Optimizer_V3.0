// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using System.Security.AccessControl;
using static Vanara.PInvoke.AdvApi32;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class AccessRuleAdvancedItem : ObservableObject
    {
        #region Humanized Strings Properties
        public string? HumanizedAccessControlType { get; set; }

        public string? HumanizedAccessControl { get; set; }

        public string? HumanizedIsInheritance { get; set; }

        public string? HumanizedAppliesTo { get; set; }
        #endregion

        #region Basic Permission Properties
        private bool _allowFullControl;
        public bool AllowFullControl { get => _allowFullControl; set => SetProperty(ref _allowFullControl, value); }

        private bool _allowRead;
        public bool AllowRead { get => _allowRead; set => SetProperty(ref _allowRead, value); }

        private bool _allowSpecialPermissions;
        public bool AllowSpecialPermissions { get => _allowSpecialPermissions; set => SetProperty(ref _allowSpecialPermissions, value); }
        #endregion

        #region Advanced Permission Properties
        private REGSAM _rawAccessMask;
        public REGSAM RawAccessMask { get => _rawAccessMask; set => SetProperty(ref _rawAccessMask, value); }

        private AceType _rawAceType;
        public AceType RawAceType { get => _rawAceType; set => SetProperty(ref _rawAceType, value); }

        private AceFlags _rawAceFlags;
        public AceFlags RawAceFlags { get => _rawAceFlags; set => SetProperty(ref _rawAceFlags, value); }
        #endregion
    }
}