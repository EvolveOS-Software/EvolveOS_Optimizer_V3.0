// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Constants;

namespace EvolveOS_Optimizer.Core.Model;

public static class WindowsThemeCustomizations
{
    public static class Wallpaper
    {
        public const string Windows11BasePath = @"C:\Windows\Web\Wallpaper\Windows";
        public const string Windows11LightWallpaper = "img0.jpg";
        public const string Windows11DarkWallpaper = "img19.jpg";
        public const string Windows10Wallpaper = @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg";

        public static string GetDefaultWallpaperPath(bool isWindows11, bool isDarkMode)
        {
            if (isWindows11)
            {
                return System.IO.Path.Combine(
                    Windows11BasePath,
                    isDarkMode ? Windows11DarkWallpaper : Windows11LightWallpaper
                );
            }

            return Windows10Wallpaper;
        }
    }

    public static SettingGroup GetWindowsThemeCustomizations()
    {
        return new SettingGroup
        {
            Name = "Windows Theme",
            FeatureId = FeatureIds.WindowsTheme,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "theme-mode-windows",
                    IsSubjectivePreference = true,
                    Name = "Choose your mode",
                    Description = "Choose between Light and Dark mode for Windows and apps",
                    GroupName = "Theme Mode",
                    InputType = InputType.Selection,
                    Icon = "BrushVariant",
                    RequiresConfirmation = true,
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                            ValueName = "AppsUseLightTheme",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                            ValueName = "SystemUsesLightTheme",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption
                            {
                                DisplayName = "Light Mode",
                                ValueMappings = new Dictionary<string, object?>
                                {
                                    ["AppsUseLightTheme"] = 1,
                                    ["SystemUsesLightTheme"] = 1,
                                },
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Dark Mode",
                                ValueMappings = new Dictionary<string, object?>
                                {
                                    ["AppsUseLightTheme"] = 0,
                                    ["SystemUsesLightTheme"] = 0,
                                },
                                IsRecommended = true,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "theme-transparency",
                    IsSubjectivePreference = true,
                    Name = "Transparency effects",
                    Description = "Enable translucent effects for the Start Menu, taskbar, and other Windows interface elements",
                    GroupName = "Transparency",
                    InputType = InputType.Toggle,
                    Icon = "Opacity",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                            ValueName = "EnableTransparency",
                            RecommendedValue = 0, // Disable transparency recommended
                            EnabledValue = [1, null], // When toggle is ON, transparency effects are enabled
                            DisabledValue = [0], // When toggle is OFF, transparency effects are disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "theme-logon-acrylic",
                    AddedInVersion = "1.1.5.331",
                    IsSubjectivePreference = true,
                    Name = "Acrylic background on sign-in screen",
                    Description = "Show a translucent acrylic blur effect on the Windows logon screen",
                    GroupName = "Transparency",
                    InputType = InputType.Toggle,
                    Icon = "Wallpaper",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                            ValueName = "DisableAcrylicBackgroundOnLogon",
                            RecommendedValue = 0,
                            EnabledValue = [0, null], // Toggle ON = Acrylic Enabled (Policy disabled/missing)
                            DisabledValue = [1],      // Toggle OFF = Acrylic Disabled (Policy active)
                            DefaultValue = 0,         // By default, Windows shows the acrylic background
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true      // It's in the Policies hive
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "theme-force-default-user-tile",
                    AddedInVersion = "1.1.5.331",
                    IsSubjectivePreference = true,
                    Name = "Force default user profile picture",
                    Description = "Forces all user accounts to use the default generic user silhouette image, preventing profile picture customization.",
                    GroupName = "Personalization",
                    InputType = InputType.Toggle,
                    Icon = "Contact",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                            ValueName = "UseDefaultTile",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                            ValueName = "UseDefaultTile",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}
