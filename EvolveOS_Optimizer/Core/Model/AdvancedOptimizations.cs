// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Enums;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Core.Model;

public static class AdvancedOptimizations
{
    public static SettingGroup GetAdvancedOptimizations()
    {
        return new SettingGroup
        {
            Name = "Advanced Settings",
            FeatureId = FeatureIds.Advanced,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "prevent-device-companion-apps",
                    IsSubjectivePreference = false,
                    AddedInVersion = "1.1.9.376",
                    Name = "Prevent Device Companion Apps",
                    Description = "Prevents additional software from being installed when plugging in devices (e.g. Ads when plugging in a monitor). Poses potential security risk.",
                    GroupName = "Essential Tweaks",
                    InputType = InputType.Toggle,
                    Icon = "Devices",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Device Metadata",
                            ValueName = "PreventDeviceMetadataFromNetwork",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "disable-wpbt-execution",
                    IsSubjectivePreference = false,
                    AddedInVersion = "1.1.9.376",
                    Name = "Disable Windows Platform Binary Table (WPBT)",
                    Description = "If enabled, WPBT allows your computer vendor to execute programs at boot time, such as anti-theft software, software drivers, as well as force install software without user consent. Poses potential security risk.",
                    GroupName = "Essential Tweaks",
                    InputType = InputType.Toggle,
                    Icon = "Shield",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager",
                            ValueName = "DisableWpbtExecution",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = false
                        }
                    }
                }
            }
        };
    }
}