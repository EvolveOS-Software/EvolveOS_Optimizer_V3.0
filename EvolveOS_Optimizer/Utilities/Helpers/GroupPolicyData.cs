using Microsoft.Win32;
using static EvolveOS_Optimizer.Utilities.Helpers.GroupPolicyHelper;

namespace EvolveOS_Optimizer.Utilities.Helpers;

internal static class GroupPolicyData
{
    internal static readonly PolicyEntry[] KnownPolicies =
        [
        new PolicyEntry
        {
            Id = "NoAutoUpdate",
            Name = "Disable Automatic Updates",
            Description = "Prevents Windows from automatically downloading and installing updates.",
            Category = "Windows Update",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
            ValueName = "NoAutoUpdate",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "AUOptions",
            Name = "Configure Automatic Updates",
            Description = "Configures how Windows handles automatic updates.",
            Category = "Windows Update",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
            ValueName = "AUOptions",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "NoAutoRebootWithLoggedOnUsers",
            Name = "No Auto-Restart With Logged On Users",
            Description = "Prevents automatic restart when users are logged on.",
            Category = "Windows Update",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
            ValueName = "NoAutoRebootWithLoggedOnUsers",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DoNotConnectToWindowsUpdateInternetLocations",
            Name = "Block Windows Update Internet Locations",
            Description = "Prevents connecting to Windows Update internet locations.",
            Category = "Windows Update",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
            ValueName = "DoNotConnectToWindowsUpdateInternetLocations",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "ExcludeWUDriversInQualityUpdate",
            Name = "Exclude Drivers From Windows Updates",
            Description = "Excludes driver updates from quality updates.",
            Category = "Windows Update",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
            ValueName = "ExcludeWUDriversInQualityUpdate",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AllowTelemetry",
            Name = "Diagnostic Data Collection",
            Description = "Controls the level of diagnostic data sent to Microsoft.",
            Category = "Privacy & Telemetry",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            ValueName = "AllowTelemetry",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DoNotShowFeedbackNotifications",
            Name = "Disable Feedback Notifications",
            Description = "Prevents Windows from showing feedback notifications.",
            Category = "Privacy & Telemetry",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            ValueName = "DoNotShowFeedbackNotifications",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DisableAdvertisingId",
            Name = "Disable Advertising ID",
            Description = "Disables the advertising ID for all users.",
            Category = "Privacy & Telemetry",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
            ValueName = "DisabledByGroupPolicy",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "CEIPEnable",
            Name = "Customer Experience Improvement Program",
            Description = "Controls participation in the Customer Experience Improvement Program.",
            Category = "Privacy & Telemetry",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\SQMClient\Windows",
            ValueName = "CEIPEnable",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "PublishUserActivities",
            Name = "Publish User Activities",
            Description = "Controls publishing of user activities.",
            Category = "Privacy & Telemetry",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\System",
            ValueName = "PublishUserActivities",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "UploadUserActivities",
            Name = "Upload User Activities",
            Description = "Controls uploading of user activities.",
            Category = "Privacy & Telemetry",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\System",
            ValueName = "UploadUserActivities",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AllowCortana",
            Name = "Allow Cortana",
            Description = "Controls whether Cortana is allowed.",
            Category = "Cortana & Search",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\Windows Search",
            ValueName = "AllowCortana",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DisableWebSearch",
            Name = "Disable Web Search",
            Description = "Disables web search in Windows Search.",
            Category = "Cortana & Search",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\Windows Search",
            ValueName = "DisableWebSearch",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "ConnectedSearchUseWeb",
            Name = "Connected Search Use Web",
            Description = "Controls connected search web usage.",
            Category = "Cortana & Search",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\Windows Search",
            ValueName = "ConnectedSearchUseWeb",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "AllowCloudSearch",
            Name = "Allow Cloud Search",
            Description = "Controls cloud search functionality.",
            Category = "Cortana & Search",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\Windows Search",
            ValueName = "AllowCloudSearch",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AutoDownload",
            Name = "Store Auto-Download",
            Description = "Controls automatic downloading of Store apps.",
            Category = "Windows Store",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\WindowsStore",
            ValueName = "AutoDownload",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DisableWindowsConsumerFeatures",
            Name = "Disable Consumer Features",
            Description = "Disables consumer features like suggested apps.",
            Category = "Windows Store",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\CloudContent",
            ValueName = "DisableWindowsConsumerFeatures",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DisableSoftLanding",
            Name = "Disable Soft Landing",
            Description = "Disables soft landing experience for new features.",
            Category = "Windows Store",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\CloudContent",
            ValueName = "DisableSoftLanding",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DisableCloudOptimizedContent",
            Name = "Disable Cloud Optimized Content",
            Description = "Disables cloud-optimized content.",
            Category = "Windows Store",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\CloudContent",
            ValueName = "DisableCloudOptimizedContent",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "DisableFileSyncNGSC",
            Name = "Disable OneDrive File Sync",
            Description = "Prevents OneDrive from syncing files.",
            Category = "OneDrive",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\OneDrive",
            ValueName = "DisableFileSyncNGSC",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "EnableSmartScreen",
            Name = "SmartScreen Filter",
            Description = "Controls the SmartScreen filter.",
            Category = "Security",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\System",
            ValueName = "EnableSmartScreen",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "DisableErrorReporting",
            Name = "Disable Error Reporting",
            Description = "Disables Windows Error Reporting.",
            Category = "Error Reporting",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting",
            ValueName = "Disabled",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DoReport",
            Name = "PC Health Error Reporting",
            Description = "Controls PC health error reporting.",
            Category = "Error Reporting",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\PCHealth\ErrorReporting",
            ValueName = "DoReport",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "DisableSR",
            Name = "Disable System Restore",
            Description = "Disables System Restore functionality.",
            Category = "System Restore",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore",
            ValueName = "DisableSR",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DisableConfig",
            Name = "Disable System Restore Configuration",
            Description = "Prevents configuration of System Restore.",
            Category = "System Restore",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore",
            ValueName = "DisableConfig",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AllowBuildPreview",
            Name = "Allow Build Preview",
            Description = "Controls Windows Insider preview builds.",
            Category = "Windows Insider",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\PreviewBuilds",
            ValueName = "AllowBuildPreview",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "EnableConfigFlighting",
            Name = "Enable Config Flighting",
            Description = "Controls configuration flighting.",
            Category = "Windows Insider",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\PreviewBuilds",
            ValueName = "EnableConfigFlighting",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "EnableExperimentation",
            Name = "Enable Experimentation",
            Description = "Controls Windows experimentation features.",
            Category = "Windows Insider",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\PreviewBuilds",
            ValueName = "EnableExperimentation",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AllowInputPersonalization",
            Name = "Allow Input Personalization",
            Description = "Controls input personalization features.",
            Category = "Input & Privacy",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\InputPersonalization",
            ValueName = "AllowInputPersonalization",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "PreventHandwritingDataSharing",
            Name = "Prevent Handwriting Data Sharing",
            Description = "Prevents sharing of handwriting data.",
            Category = "Input & Privacy",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\TabletPC",
            ValueName = "PreventHandwritingDataSharing",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "AllowLinguisticDataCollection",
            Name = "Allow Linguistic Data Collection",
            Description = "Controls linguistic data collection.",
            Category = "Input & Privacy",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\TextInput",
            ValueName = "AllowLinguisticDataCollection",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "LetAppsRunInBackground",
            Name = "Let Apps Run In Background",
            Description = "Controls whether apps can run in the background.",
            Category = "App Privacy",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
            ValueName = "LetAppsRunInBackground",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "LetAppsActivateWithVoice",
            Name = "Let Apps Activate With Voice",
            Description = "Controls voice activation for apps.",
            Category = "App Privacy",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
            ValueName = "LetAppsActivateWithVoice",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AllowWindowsInkWorkspace",
            Name = "Allow Windows Ink Workspace",
            Description = "Controls Windows Ink Workspace.",
            Category = "Windows Ink",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\WindowsInkWorkspace",
            ValueName = "AllowWindowsInkWorkspace",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "AllowSuggestedAppsInWindowsInkWorkspace",
            Name = "Allow Suggested Apps In Windows Ink",
            Description = "Controls suggested apps in Windows Ink Workspace.",
            Category = "Windows Ink",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\WindowsInkWorkspace",
            ValueName = "AllowSuggestedAppsInWindowsInkWorkspace",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "BiometricsEnabled",
            Name = "Biometrics Enabled",
            Description = "Controls biometric authentication.",
            Category = "Biometrics",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Biometrics",
            ValueName = "Enabled",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "DisableLocation",
            Name = "Disable Location Services",
            Description = "Disables location services.",
            Category = "Location",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
            ValueName = "DisableLocation",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DisableWindowsLocationProvider",
            Name = "Disable Windows Location Provider",
            Description = "Disables the Windows location provider.",
            Category = "Location",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
            ValueName = "DisableWindowsLocationProvider",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AllowFindMyDevice",
            Name = "Allow Find My Device",
            Description = "Controls Find My Device functionality.",
            Category = "Find My Device",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\FindMyDevice",
            ValueName = "AllowFindMyDevice",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AllowMessageSync",
            Name = "Allow Message Sync",
            Description = "Controls message synchronization.",
            Category = "Messaging",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\Messaging",
            ValueName = "AllowMessageSync",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AllowClipboardHistory",
            Name = "Allow Clipboard History",
            Description = "Controls clipboard history.",
            Category = "Clipboard",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\System",
            ValueName = "AllowClipboardHistory",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "AllowCrossDeviceClipboard",
            Name = "Allow Cross-Device Clipboard",
            Description = "Controls cross-device clipboard sync.",
            Category = "Clipboard",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\System",
            ValueName = "AllowCrossDeviceClipboard",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AllowSpeechModelUpdate",
            Name = "Allow Speech Model Update",
            Description = "Controls speech recognition model updates.",
            Category = "Speech",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Speech",
            ValueName = "AllowSpeechModelUpdate",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "EnableActivityFeed",
            Name = "Enable Activity Feed",
            Description = "Controls the Activity Feed feature.",
            Category = "Activity History",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\System",
            ValueName = "EnableActivityFeed",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "EnableCdp",
            Name = "Enable Connected Devices Platform",
            Description = "Controls Connected Devices Platform.",
            Category = "Activity History",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\System",
            ValueName = "EnableCdp",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "AllowGameDVR",
            Name = "Allow Game DVR",
            Description = "Controls Game DVR functionality.",
            Category = "Gaming",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\GameDVR",
            ValueName = "AllowGameDVR",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "EnableFeeds",
            Name = "Enable Windows Feeds",
            Description = "Controls Windows Feeds (News and Interests).",
            Category = "Widgets & Feeds",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds",
            ValueName = "EnableFeeds",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "AllowNewsAndInterests",
            Name = "Allow News and Interests",
            Description = "Controls the News and Interests widget.",
            Category = "Widgets & Feeds",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Dsh",
            ValueName = "AllowNewsAndInterests",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "TurnOffWindowsCopilot",
            Name = "Turn Off Windows Copilot",
            Description = "Disables Windows Copilot.",
            Category = "Copilot",
            Hive = RegistryHive.CurrentUser,
            RegistryPath = @"Software\Policies\Microsoft\Windows\WindowsCopilot",
            ValueName = "TurnOffWindowsCopilot",
            ValueKind = RegistryValueKind.DWord,
            MinWindowsBuild = 22621
        },

        new PolicyEntry
        {
            Id = "DisableAIDataAnalysis_HKLM",
            Name = "Disable Windows Recall (Machine)",
            Description = "Disables Windows Recall AI data analysis for all users.",
            Category = "Windows Recall",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
            ValueName = "DisableAIDataAnalysis",
            ValueKind = RegistryValueKind.DWord,
            MinWindowsBuild = 26100 // Windows 11 24H2+
        },
        new PolicyEntry
        {
            Id = "DisableAIDataAnalysis_HKCU",
            Name = "Disable Windows Recall (User)",
            Description = "Disables Windows Recall AI data analysis for current user.",
            Category = "Windows Recall",
            Hive = RegistryHive.CurrentUser,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
            ValueName = "DisableAIDataAnalysis",
            ValueKind = RegistryValueKind.DWord,
            MinWindowsBuild = 26100 // Windows 11 24H2+
        },

        new PolicyEntry
        {
            Id = "HubsSidebarEnabled",
            Name = "Edge Sidebar Enabled",
            Description = "Controls the Edge browser sidebar.",
            Category = "Microsoft Edge",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Edge",
            ValueName = "HubsSidebarEnabled",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "PersonalizationReportingEnabled",
            Name = "Edge Personalization Reporting",
            Description = "Controls Edge personalization reporting.",
            Category = "Microsoft Edge",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Edge",
            ValueName = "PersonalizationReportingEnabled",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "EdgeMetricsReportingEnabled",
            Name = "Edge Metrics Reporting",
            Description = "Controls Edge metrics reporting.",
            Category = "Microsoft Edge",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Edge",
            ValueName = "MetricsReportingEnabled",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "EdgeUserFeedbackAllowed",
            Name = "Edge User Feedback",
            Description = "Controls Edge user feedback.",
            Category = "Microsoft Edge",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Edge",
            ValueName = "UserFeedbackAllowed",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "EdgeSpotlightExperiences",
            Name = "Edge Spotlight Experiences",
            Description = "Controls Edge spotlight experiences and recommendations.",
            Category = "Microsoft Edge",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Edge",
            ValueName = "SpotlightExperiencesAndRecommendationsEnabled",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "FileHistoryDisabled",
            Name = "File History Disabled",
            Description = "Disables File History.",
            Category = "File History",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\FileHistory",
            ValueName = "Disabled",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "DontOfferThroughWUAU",
            Name = "Don't Offer MRT Through Windows Update",
            Description = "Prevents offering Malicious Software Removal Tool through Windows Update.",
            Category = "Security",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\MRT",
            ValueName = "DontOfferThroughWUAU",
            ValueKind = RegistryValueKind.DWord
        },

        new PolicyEntry
        {
            Id = "DisableSearchBoxSuggestions_User",
            Name = "Disable Search Box Suggestions (User)",
            Description = "Disables search box suggestions for current user.",
            Category = "Search",
            Hive = RegistryHive.CurrentUser,
            RegistryPath = @"Software\Policies\Microsoft\Windows\Explorer",
            ValueName = "DisableSearchBoxSuggestions",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "DisableSearchBoxSuggestions_Machine",
            Name = "Disable Search Box Suggestions (Machine)",
            Description = "Disables search box suggestions for all users.",
            Category = "Search",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\Explorer",
            ValueName = "DisableSearchBoxSuggestions",
            ValueKind = RegistryValueKind.DWord
        },
        new PolicyEntry
        {
            Id = "HideRecommendedSection",
            Name = "Hide Recommended Section",
            Description = "Hides the recommended section in Start Menu.",
            Category = "Start Menu",
            Hive = RegistryHive.LocalMachine,
            RegistryPath = @"SOFTWARE\Policies\Microsoft\Windows\Explorer",
            ValueName = "HideRecommendedSection",
            ValueKind = RegistryValueKind.DWord,
            MinWindowsBuild = 22000 // Windows 11+
        }
    ];
}
