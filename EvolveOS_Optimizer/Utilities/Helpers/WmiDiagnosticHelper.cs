// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License. 

using System.Management;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class WmiDiagnosticHelper
    {
        private Dictionary<int, (string Summary, string Fix)> HeuristicRuleset => new()
        {
            { 1, (ResourceString.GetString("wmi_code_1_summary") ?? "Device is not configured correctly.", ResourceString.GetString("wmi_code_1_fix") ?? "Update or roll back the driver.") },
            { 3, (ResourceString.GetString("wmi_code_3_summary") ?? "Driver for this device might be corrupted.", ResourceString.GetString("wmi_code_3_fix") ?? "Uninstall the device and scan for hardware changes.") },
            { 10, (ResourceString.GetString("wmi_code_10_summary") ?? "This device cannot start (Code 10).", ResourceString.GetString("wmi_code_10_fix") ?? "Try updating the device drivers.") },
            { 14, (ResourceString.GetString("wmi_code_14_summary") ?? "This device cannot work properly until you restart your computer.", ResourceString.GetString("wmi_code_14_fix") ?? "Restart your computer.") },
            { 18, (ResourceString.GetString("wmi_code_18_summary") ?? "Reinstall the drivers for this device.", ResourceString.GetString("wmi_code_18_fix") ?? "Click 'Update Driver' in Device Manager.") },
            { 19, (ResourceString.GetString("wmi_code_19_summary") ?? "Your registry might be corrupted.", ResourceString.GetString("wmi_code_19_fix") ?? "Windows cannot start this hardware device because its configuration information (in the registry) is incomplete or damaged.") },
            { 21, (ResourceString.GetString("wmi_code_21_summary") ?? "Windows is removing this device.", ResourceString.GetString("wmi_code_21_fix") ?? "Wait a few seconds, then refresh Device Manager.") },
            { 22, (ResourceString.GetString("wmi_code_22_summary") ?? "This device is disabled (Code 22).", ResourceString.GetString("wmi_code_22_fix") ?? "Enable the device in Device Manager.") },
            { 24, (ResourceString.GetString("wmi_code_24_summary") ?? "This device is not present, is not working properly, or does not have all its drivers installed.", ResourceString.GetString("wmi_code_24_fix") ?? "Check physical connections or reinstall drivers.") },
            { 28, (ResourceString.GetString("wmi_code_28_summary") ?? "The drivers for this device are not installed.", ResourceString.GetString("wmi_code_28_fix") ?? "Install the latest drivers from the manufacturer.") },
            { 29, (ResourceString.GetString("wmi_code_29_summary") ?? "This device is disabled because the firmware did not give it the required resources.", ResourceString.GetString("wmi_code_29_fix") ?? "Check BIOS/UEFI settings for hardware conflicts.") },
            { 31, (ResourceString.GetString("wmi_code_31_summary") ?? "This device is not working properly because Windows cannot load the drivers.", ResourceString.GetString("wmi_code_31_fix") ?? "Download and install the specific driver for this device.") },
            { 32, (ResourceString.GetString("wmi_code_32_summary") ?? "A driver (service) for this device has been disabled.", ResourceString.GetString("wmi_code_32_fix") ?? "An alternate driver may be providing this functionality.") },
            { 37, (ResourceString.GetString("wmi_code_37_summary") ?? "Windows cannot initialize the device driver for this hardware.", ResourceString.GetString("wmi_code_37_fix") ?? "Uninstall the device and restart the PC.") },
            { 38, (ResourceString.GetString("wmi_code_38_summary") ?? "Windows cannot load the device driver for this hardware because a previous instance of the device driver is still in memory.", ResourceString.GetString("wmi_code_38_fix") ?? "Restart your computer.") },
            { 39, (ResourceString.GetString("wmi_code_39_summary") ?? "Windows cannot load the device driver for this hardware. The driver may be corrupted or missing.", ResourceString.GetString("wmi_code_39_fix") ?? "Reinstall the driver.") },
            { 41, (ResourceString.GetString("wmi_code_41_summary") ?? "Windows successfully loaded the device driver, but cannot find the hardware device.", ResourceString.GetString("wmi_code_41_fix") ?? "Check if the device is plugged in properly.") },
            { 43, (ResourceString.GetString("wmi_code_43_summary") ?? "Windows has stopped this device because it has reported problems (Code 43).", ResourceString.GetString("wmi_code_43_fix") ?? "This often indicates a hardware failure or a deeply corrupted driver.") },
            { 45, (ResourceString.GetString("wmi_code_45_summary") ?? "Currently, this hardware device is not connected to the computer.", ResourceString.GetString("wmi_code_45_fix") ?? "Reconnect the device to the computer.") },
            { 47, (ResourceString.GetString("wmi_code_47_summary") ?? "Windows cannot use this hardware device because it has been prepared for safe removal.", ResourceString.GetString("wmi_code_47_fix") ?? "Unplug the device and plug it back in.") },
            { 48, (ResourceString.GetString("wmi_code_48_summary") ?? "The software for this device has been blocked from starting because it is known to have problems with Windows.", ResourceString.GetString("wmi_code_48_fix") ?? "Check for an updated driver from the manufacturer.") },
            { 52, (ResourceString.GetString("wmi_code_52_summary") ?? "Windows cannot verify the digital signature for the drivers required for this device.", ResourceString.GetString("wmi_code_52_fix") ?? "You may need to disable Driver Signature Enforcement or download a signed driver.") }
        };

        public async Task<List<HardwareIssue>> ListBrokenHardwareAsync()
        {
            var results = new List<HardwareIssue>();

            await Task.Run(() =>
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity"))
                    using (var collection = searcher.Get())
                    {
                        foreach (var device in collection)
                        {
                            object errorCodeObj = device["ConfigManagerErrorCode"];
                            if (errorCodeObj == null) continue;

                            int code = Convert.ToInt32(errorCodeObj);

                            if (code == 0) continue;

                            string deviceName = device["Name"]?.ToString() ?? ResourceString.GetString("wmi_unknown_device") ?? "Unknown Device";
                            string? deviceId = device["PNPDeviceID"]?.ToString();

                            string hardwareType = ResourceString.GetString("hw_generic") ?? "Generic Controller";

                            if (deviceName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || deviceName.Contains("Radeon", StringComparison.OrdinalIgnoreCase) || deviceName.Contains("Display Adapter", StringComparison.OrdinalIgnoreCase))
                                hardwareType = ResourceString.GetString("hw_gpu") ?? "Graphics Processor";
                            else if (deviceName.Contains("USB", StringComparison.OrdinalIgnoreCase) || deviceName.Contains("Hub", StringComparison.OrdinalIgnoreCase))
                                hardwareType = ResourceString.GetString("hw_usb") ?? "USB Host/Hub";
                            else if (deviceName.Contains("Audio", StringComparison.OrdinalIgnoreCase) || deviceName.Contains("High Definition", StringComparison.OrdinalIgnoreCase))
                                hardwareType = ResourceString.GetString("hw_audio") ?? "Sound Device";
                            else if (deviceName.Contains("Network Adapter", StringComparison.OrdinalIgnoreCase) || deviceName.Contains("Wireless", StringComparison.OrdinalIgnoreCase))
                                hardwareType = ResourceString.GetString("hw_network") ?? "Network Adapter";
                            else if (deviceName.Contains("PCI", StringComparison.OrdinalIgnoreCase) || deviceName.Contains("System", StringComparison.OrdinalIgnoreCase))
                                hardwareType = ResourceString.GetString("hw_system") ?? "System Device";
                            else if (deviceName.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
                                hardwareType = ResourceString.GetString("hw_bluetooth") ?? "Bluetooth Radio";

                            string summary = string.Format(ResourceString.GetString("wmi_code_default_summary") ?? "A cryptic error code (Code {0}) was detected by the proactive scan.", code);
                            string fix = ResourceString.GetString("wmi_code_default_fix") ?? "Windows encountered an internal error with this driver. Check Device Manager or reinstall the standard driver.";

                            if (HeuristicRuleset.TryGetValue(code, out var ruleMatch))
                            {
                                summary = ruleMatch.Summary;
                                fix = ruleMatch.Fix;
                            }
                            else if (code > 52)
                            {
                                summary = string.Format(ResourceString.GetString("wmi_code_extended_summary") ?? "Extended PnP Error (Code {0}) detected on the hardware bus.", code);
                                fix = ResourceString.GetString("wmi_code_extended_fix") ?? "This is a complex hardware conflict or driver crash. Use the EvolveOS auto-remediation tool to reset the PnP state.";
                            }

                            results.Add(new HardwareIssue
                            {
                                WmiErrorCode = code,
                                DeviceName = deviceName,
                                DeviceId = deviceId,
                                ComponentDisplayName = deviceName,
                                HardwareType = hardwareType,
                                IssueSummary = summary,
                                RecommendedFix = fix
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WMI Diagnostic Helper Error] {ex.Message}");
                }
            });

            return results;
        }
    }
}