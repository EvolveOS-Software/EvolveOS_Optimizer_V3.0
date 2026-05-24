// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using static Vanara.PInvoke.AdvApi32;

namespace EvolveOS_Optimizer.Core.Model
{
    public class PermissionPrincipalItem
    {
        #region SID Identity
        public SID_NAME_USE SidType { get; set; }

        public string SidTypeImagePath
        {
            get
            {
                switch (SidType)
                {
                    case SID_NAME_USE.SidTypeAlias:
                    case SID_NAME_USE.SidTypeGroup:
                    case SID_NAME_USE.SidTypeWellKnownGroup:
                        return "ms-appx:///Assets/PngImages/GroupImage.png";
                    case SID_NAME_USE.SidTypeUser:
                        return "ms-appx:///Assets/PngImages/UserImage.png";
                    default:
                        return "ms-appx:///Assets/PngImages/UnknownImage.png";
                }
            }
        }

        public string? Sid { get; set; }
        public string? Domain { get; set; }
        public string? Name { get; set; }
        #endregion

        #region Formatting
        public string? DisplayName
            => string.IsNullOrEmpty(Name) ? Sid : Name;

        public string FullName
            => string.IsNullOrEmpty(Domain) ? string.Empty : $"{Domain}\\{Name}";
        #endregion

        #region Access Control
        public AccessRuleAdvancedItem? AccessRuleAdvanced { get; set; }
        public AccessRuleMergedItem? AccessRuleMerged { get; set; }
        #endregion
    }
}
