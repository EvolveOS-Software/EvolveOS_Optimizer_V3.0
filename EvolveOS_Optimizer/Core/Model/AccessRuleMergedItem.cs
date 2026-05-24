// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using static Vanara.PInvoke.AdvApi32;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class AccessRuleMergedItem : ObservableObject
    {
        public REGSAM MaskAllowed { get; set; }
        public REGSAM MaskDenied { get; set; }

        private bool _isInheritedDeniedMask;
        public bool IsInheritedDeniedMask { get => _isInheritedDeniedMask; set => SetProperty(ref _isInheritedDeniedMask, value); }

        private bool _isInheritedAllowedMask;
        public bool IsInheritedAllowedMask { get => _isInheritedAllowedMask; set => SetProperty(ref _isInheritedAllowedMask, value); }

        #region Permission Properties

        private bool _allowFullControl;
        public bool AllowFullControl { get => _allowFullControl; set => SetProperty(ref _allowFullControl, value); }

        private bool _allowRead;
        public bool AllowRead { get => _allowRead; set => SetProperty(ref _allowRead, value); }

        private bool _allowSpecialPermissions;
        public bool AllowSpecialPermissions { get => _allowSpecialPermissions; set => SetProperty(ref _allowSpecialPermissions, value); }

        private bool _denyFullControl;
        public bool DenyFullControl { get => _denyFullControl; set => SetProperty(ref _denyFullControl, value); }

        private bool _denyRead;
        public bool DenyRead { get => _denyRead; set => SetProperty(ref _denyRead, value); }

        private bool _denySpecialPermissions;
        public bool DenySpecialPermissions { get => _denySpecialPermissions; set => SetProperty(ref _denySpecialPermissions, value); }

        #endregion
    }
}