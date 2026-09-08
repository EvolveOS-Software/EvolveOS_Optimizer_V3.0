// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class IdentityScanResult
    {
        public bool IsHostsBlocked { get; set; }
        public List<FoundIdentityItem> FoundTokens { get; set; } = new();
        public List<string> VaultStatus { get; set; } = new();
        public bool IsAdmin { get; set; }
        public string OsVersion { get; set; } = string.Empty;
    }

    public class FoundIdentityItem
    {
        public string HiveName { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public string TokenValue { get; set; } = string.Empty;
    }

    public static class DeviceIdentityEngine
    {
        private static readonly string MarkerBegin = "# BEGIN degdid-registration-block";
        private static readonly string MarkerEnd = "# END degdid-registration-block";
        private static readonly string HostsPath = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
        private static readonly string MintHost = "login.live.com";

        private static readonly string[] BlockHosts = new[]
        {
            "login.live.com", "account.live.com", "cs.dds.microsoft.com", "dds.microsoft.com",
            "aad.cs.dds.microsoft.com", "fd.dds.microsoft.com", "cdpcs.access.microsoft.com",
            "ztd.dds.microsoft.com", "activity.windows.com", "assets.activity.windows.com", "edge.activity.windows.com"
        };

        private static readonly string[] DeviceCredentialTargets = new[]
        {
            "MicrosoftAccount:target=SSO_POP_Device",
            "WindowsLive:target=virtualapp/didlogical"
        };

        public static async Task<IdentityScanResult> InspectStatusDetailedAsync()
        {
            return await Task.Run(() =>
            {
                var result = new IdentityScanResult
                {
                    IsAdmin = IsAdministrator(),
                    OsVersion = Environment.OSVersion.ToString()
                };

                // 1. Hosts Block Check
                try
                {
                    if (File.Exists(HostsPath))
                    {
                        string hostsContent = File.ReadAllText(HostsPath);
                        result.IsHostsBlocked = hostsContent.Contains(MarkerBegin) && hostsContent.Contains(MintHost);
                    }
                }
                catch { }

                // 2. Registry GDID & Identity Store Scan
                try
                {
                    foreach (string subKeyName in Registry.Users.GetSubKeyNames())
                    {
                        if (subKeyName.StartsWith("S-1-5-21-") || subKeyName == ".DEFAULT" || subKeyName == "S-1-5-18")
                        {
                            string identityPath = $@"{subKeyName}\SOFTWARE\Microsoft\IdentityCRL\ExtendedProperties";
                            using (var key = Registry.Users.OpenSubKey(identityPath))
                            {
                                if (key != null)
                                {
                                    object? lid = key.GetValue("LID");
                                    if (lid != null)
                                    {
                                        result.FoundTokens.Add(new FoundIdentityItem
                                        {
                                            HiveName = subKeyName,
                                            TokenType = "ExtendedProperties (LID)",
                                            TokenValue = lid.ToString() ?? string.Empty
                                        });
                                    }
                                }
                            }

                            string immersivePath = $@"{subKeyName}\SOFTWARE\Microsoft\IdentityCRL\Immersive\production\Property";
                            using (var key = Registry.Users.OpenSubKey(immersivePath))
                            {
                                if (key != null)
                                {
                                    foreach (string valueName in key.GetValueNames())
                                    {
                                        if (Regex.IsMatch(valueName, @"(?i)^0018[0-9A-Fa-f]{12}$"))
                                        {
                                            result.FoundTokens.Add(new FoundIdentityItem
                                            {
                                                HiveName = subKeyName,
                                                TokenType = "Immersive PUID Property",
                                                TokenValue = valueName
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                // 3. Credential Manager Vault Check
                try
                {
                    foreach (var target in DeviceCredentialTargets)
                    {
                        bool present = CheckCmdKeyCredential(target);
                        string targetName = target.Split('=')[1];
                        result.VaultStatus.Add($"{targetName}: {(present ? "Present (Rehydration Active)" : "Clean")}");
                    }
                }
                catch { }

                return result;
            });
        }

        public static async Task<string> InspectStatusAsync()
        {
            var res = await InspectStatusDetailedAsync();
            var sb = new StringBuilder();
            sb.AppendLine($"=== EvolveOS Device Identity Diagnostics — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            sb.AppendLine($"[Environment] Admin: {res.IsAdmin}, OS: {res.OsVersion}");
            sb.AppendLine($"[Hosts Block] {(res.IsHostsBlocked ? "ACTIVE" : "NOT CONFIGURED")}");
            sb.AppendLine($"[Found Identifiers] Total: {res.FoundTokens.Count}");
            foreach (var token in res.FoundTokens)
            {
                sb.AppendLine($"  - [{token.HiveName}] {token.TokenType}: {token.TokenValue}");
            }
            return sb.ToString();
        }

        public static async Task<string> ProtectAndWipeAsync()
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== EvolveOS Protect & Wipe Sequence ===");
                ApplyHostsBlockInternal(sb);
                FlushDnsCache(sb);
                PurgeRegistryGdidsInternal(sb);
                PurgeDeviceCredentialsInternal(sb);
                PurgeLocalCachesInternal(sb);
                sb.AppendLine("\nProtect & Wipe sequence executed successfully.");
                return sb.ToString();
            });
        }

        public static async Task<string> ProtectAndDecoyAsync()
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== EvolveOS Protect & Decoy Sequence ===");
                ApplyHostsBlockInternal(sb);
                FlushDnsCache(sb);
                string decoyLid = GenerateDecoyLid();
                sb.AppendLine($"  Generated Decoy ID: {decoyLid}");
                InjectDecoyInternal(sb, decoyLid);
                sb.AppendLine("\nProtect & Decoy sequence executed successfully.");
                return sb.ToString();
            });
        }

        public static async Task<string> UnblockNetworkAsync()
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== EvolveOS Network Unblock Sequence ===");
                RemoveHostsBlockInternal(sb);
                FlushDnsCache(sb);
                sb.AppendLine("\nNetwork blocks removed successfully.");
                return sb.ToString();
            });
        }

        #region Internal Helper Implementations

        private static bool IsAdministrator()
        {
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        private static void ApplyHostsBlockInternal(StringBuilder sb)
        {
            try
            {
                string text = File.Exists(HostsPath) ? File.ReadAllText(HostsPath) : string.Empty;
                if (text.Contains(MarkerBegin)) return;

                var blockLines = new List<string> { string.Empty, MarkerBegin };
                foreach (var host in BlockHosts)
                {
                    blockLines.Add($"0.0.0.0 {host}");
                    blockLines.Add($":: {host}");
                }
                blockLines.Add(MarkerEnd);
                File.AppendAllText(HostsPath, string.Join(Environment.NewLine, blockLines), Encoding.UTF8);
                sb.AppendLine("  Canonical hosts region successfully written.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ERROR writing hosts block: {ex.Message}");
                throw;
            }
        }

        private static void RemoveHostsBlockInternal(StringBuilder sb)
        {
            try
            {
                if (!File.Exists(HostsPath)) return;
                string text = File.ReadAllText(HostsPath);
                int startIndex = text.IndexOf(MarkerBegin);
                int endIndex = text.IndexOf(MarkerEnd);

                if (startIndex >= 0 && endIndex >= startIndex)
                {
                    int length = endIndex + MarkerEnd.Length;
                    if (length < text.Length && text[length] == '\r') length++;
                    if (length < text.Length && text[length] == '\n') length++;
                    File.WriteAllText(HostsPath, text.Remove(startIndex, length - startIndex), Encoding.UTF8);
                    sb.AppendLine("  Managed hosts region removed.");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ERROR removing hosts block: {ex.Message}");
                throw;
            }
        }

        private static void FlushDnsCache(StringBuilder sb)
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = "ipconfig.exe", Arguments = "/flushdns", CreateNoWindow = true, UseShellExecute = false };
                using (var p = Process.Start(psi)) { p?.WaitForExit(); }
                sb.AppendLine("  DNS cache successfully flushed.");
            }
            catch (Exception ex)
            {
                sb.AppendLine("  Warning: Failed to flush DNS cache: " + ex.Message);
            }
        }

        private static void PurgeRegistryGdidsInternal(StringBuilder sb)
        {
            foreach (string subKeyName in Registry.Users.GetSubKeyNames())
            {
                if (subKeyName.StartsWith("S-1-5-21-") || subKeyName == ".DEFAULT" || subKeyName == "S-1-5-18")
                {
                    try
                    {
                        string identityPath = $@"{subKeyName}\SOFTWARE\Microsoft\IdentityCRL\ExtendedProperties";
                        using (var key = Registry.Users.OpenSubKey(identityPath, true))
                        {
                            key?.DeleteValue("LID", false);
                        }

                        string immersivePath = $@"{subKeyName}\SOFTWARE\Microsoft\IdentityCRL\Immersive\production\Property";
                        using (var key = Registry.Users.OpenSubKey(immersivePath, true))
                        {
                            if (key != null)
                            {
                                foreach (string valName in key.GetValueNames())
                                {
                                    key.DeleteValue(valName, false);
                                }
                            }
                        }
                        sb.AppendLine($"  Purged identity registry keys for hive [{subKeyName}]");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  Note on hive [{subKeyName}]: {ex.Message}");
                    }
                }
            }
        }

        private static void InjectDecoyInternal(StringBuilder sb, string decoyLid)
        {
            PurgeRegistryGdidsInternal(sb);
            foreach (string subKeyName in Registry.Users.GetSubKeyNames())
            {
                if (subKeyName.StartsWith("S-1-5-21-") || subKeyName == ".DEFAULT" || subKeyName == "S-1-5-18")
                {
                    try
                    {
                        string identityPath = $@"{subKeyName}\SOFTWARE\Microsoft\IdentityCRL\ExtendedProperties";
                        using (var key = Registry.Users.CreateSubKey(identityPath))
                        {
                            key?.SetValue("LID", decoyLid, RegistryValueKind.String);
                        }
                        sb.AppendLine($"  Injected decoy LID into hive [{subKeyName}]");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  Note on decoy injection [{subKeyName}]: {ex.Message}");
                    }
                }
            }
        }

        private static void PurgeDeviceCredentialsInternal(StringBuilder sb)
        {
            foreach (var target in DeviceCredentialTargets)
            {
                try
                {
                    string targetName = target.Split('=')[1];
                    var psi = new ProcessStartInfo { FileName = "cmdkey.exe", Arguments = $"/delete:{targetName}", CreateNoWindow = true, UseShellExecute = false };
                    using (var p = Process.Start(psi)) { p?.WaitForExit(); }
                    sb.AppendLine($"  Deleted credential vault target: {targetName}");
                }
                catch { }
            }
        }

        private static bool CheckCmdKeyCredential(string targetName)
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = "cmdkey.exe", Arguments = "/list", CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                using (var p = Process.Start(psi))
                {
                    string output = p?.StandardOutput.ReadToEnd() ?? string.Empty;
                    p?.WaitForExit();
                    return output.IndexOf(targetName.Split('=')[1], StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }

        private static void PurgeLocalCachesInternal(StringBuilder sb)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string tokenBrokerPath = Path.Combine(localAppData, @"Microsoft\TokenBroker\Cache");
                string cdpPath = Path.Combine(localAppData, "ConnectedDevicesPlatform");

                if (Directory.Exists(tokenBrokerPath)) Directory.Delete(tokenBrokerPath, true);
                if (Directory.Exists(cdpPath)) Directory.Delete(cdpPath, true);
            }
            catch { }
        }

        private static string GenerateDecoyLid()
        {
            byte[] bytes = new byte[6];
            using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(bytes); }
            var sb = new StringBuilder("0018");
            foreach (byte b in bytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        #endregion
    }
}