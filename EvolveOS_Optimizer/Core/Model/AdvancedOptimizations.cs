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
                    Id = "device-companion-apps",
                    IsSubjectivePreference = false,
                    AddedInVersion = "1.1.9.376",
                    Name = "Device Companion Apps",
                    Description = "When enabled, allows additional software to be installed when plugging in devices (e.g. Ads when plugging in a monitor). Disabling this prevents potential security risks.",
                    GroupName = "Essential Tweaks",
                    InputType = InputType.Toggle,
                    Icon = "Devices",
                    RecommendedToggleState = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Device Metadata",
                            ValueName = "PreventDeviceMetadataFromNetwork",
                            RecommendedValue = 1,
                            EnabledValue = [0, null], // 0 = Feature Allowed
                            DisabledValue = [1],      // 1 = Feature Prevented
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "wpbt-execution",
                    IsSubjectivePreference = false,
                    AddedInVersion = "1.1.9.376",
                    Name = "Windows Platform Binary Table (WPBT)",
                    Description = "When enabled, WPBT allows your computer vendor to execute programs at boot time, such as anti-theft software and drivers, sometimes without user consent. Disabling this removes a potential security risk.",
                    GroupName = "Essential Tweaks",
                    InputType = InputType.Toggle,
                    Icon = "Shield",
                    RecommendedToggleState = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager",
                            ValueName = "DisableWpbtExecution",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "razer-auto-install",
                    IsSubjectivePreference = true,
                    AddedInVersion = "1.1.9.376",
                    Name = "Razer Software Auto-Install",
                    Description = "When enabled, Windows will automatically download and install Razer bloatware when a Razer device is connected. Disabling this blocks the installation while keeping hardware fully functional.",
                    GroupName = "Essential Tweaks",
                    InputType = InputType.Toggle,
                    Icon = "Block",
                    RecommendedToggleState = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching",
                            ValueName = "SearchOrderConfig",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Installer",
                            ValueName = "DisableCoInstallers",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord
                        }
                    },
                    PowerShellScripts = new List<PowerShellScriptSetting>
                    {
                        new PowerShellScriptSetting
                        {
                            // Toggle ON = Allow Razer (Removes the deny permission)
                            EnabledScript = "icacls \"$Env:SystemRoot\\Installer\\Razer\" /remove:d Everyone",
            
                            // Toggle OFF = Block Razer (Creates folder and denies write permissions)
                            DisabledScript = "$RazerPath = \"$Env:SystemRoot\\Installer\\Razer\"; if (Test-Path $RazerPath) { Remove-Item $RazerPath\\* -Recurse -Force } else { New-Item -Path $RazerPath -ItemType Directory -Force }; icacls $RazerPath /deny \"Everyone:(W)\"",

                            RequiresElevation = true,
                            RunContext = RunContext.System
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "llmnr",
                    IsSubjectivePreference = false,
                    AddedInVersion = "1.1.9.376",
                    Name = "Link-Local Multicast Name Resolution (LLMNR)",
                    Description = "When enabled, Windows broadcasts unencrypted DNS requests to the local network as a fallback. Disabling this is highly recommended to block common local network credential theft attacks (e.g., Responder).",
                    GroupName = "Essential Tweaks",
                    InputType = InputType.Toggle,
                    Icon = "ShieldNetwork",
                    RecommendedToggleState = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient",
                            ValueName = "EnableMulticast",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "windows-copilot",
                    IsSubjectivePreference = true,
                    AddedInVersion = "1.1.9.376",
                    Name = "Windows Copilot",
                    Description = "When enabled, the Windows Copilot AI assistant is active on the taskbar and runs background processes. Disabling this completely removes the feature for better privacy and performance.",
                    GroupName = "Essential Tweaks",
                    InputType = InputType.Toggle,
                    Icon = "RobotOff",
                    RecommendedToggleState = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\WindowsCopilot",
                            ValueName = "TurnOffWindowsCopilot",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot",
                            ValueName = "TurnOffWindowsCopilot",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "windows-error-reporting",
                    IsSubjectivePreference = true,
                    AddedInVersion = "1.1.9.376",
                    Name = "Windows Error Reporting (WER)",
                    Description = "When enabled, Windows generates memory dumps and sends crash telemetry to Microsoft when an application crashes. Disabling this saves disk space and improves privacy.",
                    GroupName = "Essential Tweaks",
                    InputType = InputType.Toggle,
                    Icon = "Bug",
                    RecommendedToggleState = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting",
                            ValueName = "Disabled",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "autorun",
                    IsSubjectivePreference = false,
                    AddedInVersion = "1.1.9.376",
                    Name = "AutoPlay / AutoRun",
                    Description = "When enabled, removable drives, CDs, and mounted ISOs can automatically execute files upon connection. Disabling this provides a critical defense against malicious USB drives.",
                    GroupName = "Essential Tweaks",
                    InputType = InputType.Toggle,
                    Icon = "UsbFlashDrive",
                    RecommendedToggleState = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                            ValueName = "NoDriveTypeAutoRun",
                            RecommendedValue = 255,
                            EnabledValue = [145, null],
                            DisabledValue = [255],
                            DefaultValue = 145,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "activity-history",
                    IsSubjectivePreference = true,
                    AddedInVersion = "1.1.9.376",
                    Name = "Activity History",
                    Description = "When enabled, Windows tracks the files, apps, and websites you open to sync them across your Microsoft account. Disabling this improves local privacy.",
                    GroupName = "Essential Tweaks",
                    InputType = InputType.Toggle,
                    Icon = "History",
                    RecommendedToggleState = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                            ValueName = "PublishUserActivities",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "defender-cpu-limit",
                    IsSubjectivePreference = true,
                    AddedInVersion = "1.1.9.376",
                    Name = "Defender Background Scan CPU Limit (10%)",
                    Description = "When enabled, this tweak caps Windows Defender's background scan CPU usage to 10% (default is 50%) to prevent random system slowdowns and stuttering during gameplay.",
                    GroupName = "Windows Defender (Security)",
                    InputType = InputType.Toggle,
                    Icon = "ShieldHalfFull",
                    RecommendedToggleState = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Scan",
                            ValueName = "AvgCPULoadFactor",
                            RecommendedValue = 10,
                            EnabledValue = [10],
                            DisabledValue = [50, 0, null],
                            DefaultValue = 50,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "defender-scheduled-scan",
                    IsSubjectivePreference = true,
                    AddedInVersion = "1.2.4.413",
                    Name = "Disable Scheduled System Scans",
                    Description = "Disables Windows Defender's automatic periodic background system scans to prevent sudden high CPU usage and stutters while keeping real-time protection active.",
                    Warning = "WARNING: You MUST manually turn off 'Tamper Protection' in Windows Security first, or Windows will block this change.",
                    GroupName = "Windows Defender (Security)",
                    InputType = InputType.Toggle,
                    Icon = "CalendarRemove",
                    RecommendedToggleState = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Scan",
                            ValueName = "ScheduleScanDay",
                            RecommendedValue = 8,
                            EnabledValue = [8],
                            DisabledValue = [0, null],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "defender-cloud-sample",
                    IsSubjectivePreference = true,
                    AddedInVersion = "1.1.9.376",
                    Name = "Defender Cloud Protection & Sample Submission",
                    Description = "When enabled, Windows Defender constantly sends file samples and telemetry to Microsoft's cloud. Disabling this reduces network I/O and improves privacy.",
                    GroupName = "Windows Defender (Security)",
                    InputType = InputType.Toggle,
                    Icon = "CloudOff",
                    RecommendedToggleState = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Spynet",
                            ValueName = "SpynetReporting",
                            RecommendedValue = 0,
                            EnabledValue = [1, 2, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Spynet",
                            ValueName = "SubmitSamplesConsent",
                            RecommendedValue = 2,
                            EnabledValue = [1, 3, 0, null],
                            DisabledValue = [2],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "defender-realtime-protection",
                    IsSubjectivePreference = true,
                    AddedInVersion = "1.1.9.376",
                    Name = "Real-Time Protection",
                    Description = "When enabled, Defender actively scans files for threats. Disabling this provides extreme performance gains.",
                    Warning = "WARNING: You MUST manually turn off 'Tamper Protection' in Windows Security first, or Windows will block this change.",
                    GroupName = "Windows Defender (Security)",
                    InputType = InputType.Toggle,
                    Icon = "ShieldOff",
                    RecommendedToggleState = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection",
                            ValueName = "DisableRealtimeMonitoring",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true
                        }
                    }
                }
            }
        };
    }
}