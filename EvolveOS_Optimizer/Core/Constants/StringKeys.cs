// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Core.Constants;

public static class StringKeys
{
    #region Global UI Keys
    public static class Buttons
    {
        public const string OK = "Button_OK";
        public const string Cancel = "Button_Cancel";
        public const string Continue = "Button_Continue";
        public const string Yes = "Button_Yes";
        public const string No = "Button_No";
        public const string Close = "Button_Close";
        public const string Import = "Button_Import";
        public const string Export = "Button_Export";
    }

    public static class Themes
    {
        public const string System = "Theme_System";
        public const string LightNative = "Theme_LightNative";
        public const string DarkNative = "Theme_DarkNative";
    }
    #endregion

    #region View & Action Keys
    public static class View
    {
        public const string Menu = "View_Menu";
        public const string TechnicalDetails = "View_TechnicalDetails";
        public const string InfoBadges = "View_InfoBadges";
        public const string NewBadges = "View_NewBadges";
        public const string NewBadgesTooltip = "View_NewBadges_Tooltip";
        public const string ShowOnlyChanges = "View_ShowOnlyChanges";
        public const string ShowOnlyChangesTooltip = "View_ShowOnlyChanges_Tooltip";
    }

    public static class QuickActions
    {
        public const string Menu = "QuickActions_Menu";
        public const string ApplyRecommended = "QuickActions_ApplyRecommended";
        public const string ResetDefaults = "QuickActions_ResetDefaults";
        public const string ConfirmTitle = "QuickActions_ConfirmTitle";
        public const string ConfirmMessage = "QuickActions_ConfirmMessage";
        public const string SuccessMessage = "QuickActions_SuccessMessage";
        public const string AcceptAll = "QuickActions_AcceptAll";
        public const string RejectAll = "QuickActions_RejectAll";
        public const string AcceptConfirmMessage = "QuickActions_AcceptConfirmMessage";
        public const string RejectConfirmMessage = "QuickActions_RejectConfirmMessage";
    }
    #endregion

    #region Component Keys
    public static class InfoBadge
    {
        public const string Recommended = "InfoBadge_Recommended";
        public const string Default = "InfoBadge_Default";
        public const string Custom = "InfoBadge_Custom";
        public const string Preference = "InfoBadge_Preference";
        public const string RecommendedTooltip = "InfoBadge_Recommended_Tooltip";
        public const string DefaultTooltip = "InfoBadge_Default_Tooltip";
        public const string CustomTooltip = "InfoBadge_Custom_Tooltip";
        public const string PreferenceTooltip = "InfoBadge_Preference_Tooltip";

        public const string NumericSetToRecommendedTooltip = "InfoBadge_Numeric_SetToRecommended_Tooltip";
        public const string NumericSetToDefaultTooltip = "InfoBadge_Numeric_SetToDefault_Tooltip";
    }
    #endregion

    #region Localization Service Wrapper
    public static class Localized
    {
        #region Service Management
        private static ILocalizationService? _service;

        public static void Initialize(ILocalizationService service) => _service = service;

        private static string Get(string key) => _service?.GetString(key) ?? key;
        #endregion

        #region Quick Access Properties
        public static string Dialog_Confirmation => Get("Dialog_Confirmation");

        public static string Button_OK => Get(Buttons.OK);
        public static string Button_Cancel => Get(Buttons.Cancel);
        public static string Button_Continue => Get(Buttons.Continue);
        public static string Button_Yes => Get(Buttons.Yes);
        public static string Button_No => Get(Buttons.No);
        #endregion
    }
    #endregion
}