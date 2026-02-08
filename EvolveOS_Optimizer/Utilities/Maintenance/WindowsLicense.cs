using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Storage;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EvolveOS_Optimizer.Utilities.Maintenance
{
    internal sealed class WindowsLicense : WinKeyStorage
    {
        internal static bool IsWindowsActivated = false;

        private static bool IsKeyExists(string pattern, byte words) => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled).Matches(HardwareData.OS.Name).Count == words;

        internal static async void LicenseStatus()
        {
            try
            {
                using ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\cimv2", "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE ApplicationID = '55c92734-d682-4d71-983e-d6ec3f16059f' and LicenseStatus = 1");
                foreach (ManagementObject managementObj in searcher.Get().Cast<ManagementObject>())
                {
                    using (managementObj)
                    {
                        IsWindowsActivated = (uint)managementObj["LicenseStatus"] == 1;
                    }
                }
            }
            catch (COMException)
            {
                try
                {
                    string output = await CommandExecutor.GetCommandOutput("cscript //B \"$env:windir\\system32\\slmgr.vbs\" /ato; $code = $LASTEXITCODE; ($code -eq 0 -or $code -eq -2147024773)", true);
                    bool.TryParse(output, out IsWindowsActivated);
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
        }

        internal static async Task StartActivation()
        {
            string keyWinHWID = keysHWID.FirstOrDefault(k => IsKeyExists(k.Key.pattern, k.Key.words)).Value ?? string.Empty;
            string keyWinKMS = keysKMS.FirstOrDefault(k => IsKeyExists(k.Key.pattern, k.Key.words)).Value ?? string.Empty;

            if (string.IsNullOrEmpty(keyWinHWID) && string.IsNullOrEmpty(keyWinKMS))
            {
                // NotificationManager.Show("warn", "keynotfound_noty").WithDelay(300).Perform();
                return;
            }

            // NotificationManager.Show("warn", "win_activate_noty").Perform();

            // OverlayWindow overlayWindow = new OverlayWindow();
            // overlayWindow.Show();

            try
            {
                if (HardwareData.OS.IsWin10)
                {
                    await CommandExecutor.InvokeRunCommand("/c " + CommandExecutor.CleanCommand(string.Join(" & ", new[] { "assoc .vbs=VBSFile", "ftype VBSFile=\"%SystemRoot%\\System32\\WScript.exe\" \"%1\" %*" })));
                }

                await CommandExecutor.InvokeRunCommand($"/c slmgr.vbs //b /ipk {keyWinHWID}");

                await CommandExecutor.RunCommand($@"/c del /f /q {PathLocator.Folders.SystemDrive}ProgramData\Microsoft\Windows\ClipSVC\GenuineTicket\*.xml & del /f /q {PathLocator.Folders.SystemDrive}ProgramData\Microsoft\Windows\ClipSVC\Install\Migration\*.xml");

                string originalGeo = RegistryHelp.GetValue(@"HKEY_CURRENT_USER\Control Panel\International\Geo", "Name", CultureInfo.InstalledUICulture.Name.Split('-')[1].ToUpperInvariant()) ?? "US";
                RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\International\Geo", "Name", "US", RegistryValueKind.String);

                string svcRestartCmd = string.Join(" & ", new[] { "ClipSVC", "wlidsvc", "sppsvc", "KeyIso", "LicenseManager", "Winmgmt" }.Select(service => $"sc config {service} start= auto && sc start {service}"));
                await CommandExecutor.InvokeRunCommand($"/c {svcRestartCmd}");

                await Task.Delay(3000);

                XDocument xmlDoc;
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = "EvolveOS_Optimizer.Tickets.xml";

                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        throw new FileNotFoundException($"Could not find embedded resource: {resourceName}");

                    xmlDoc = XDocument.Load(stream);
                }

                // 1. Fixed: Handle potential null when searching for the ticket
                XElement? foundTicket = xmlDoc.Descendants("Ticket").FirstOrDefault(t =>
                    t.Element("product") != null &&
                    t.Element("product")!.Value.IndexOf(RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ProductOptions", "OSProductPfn", string.Empty) ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0);

                foundTicket ??= xmlDoc.Descendants("Ticket").FirstOrDefault(t =>
                    t.Element("product") != null &&
                    t.Element("product")!.Value == "KMS");

                // 2. Fixed: Verify foundTicket and content are not null before Parsing
                string? ticketContent = foundTicket?.Element("content")?.Value?.Trim();

                if (!string.IsNullOrEmpty(ticketContent))
                {
                    XDocument genuineXml = XDocument.Parse(ticketContent);
                    string ticketPath = Path.Combine(PathLocator.Folders.SystemDrive, "ProgramData", "Microsoft", "Windows", "ClipSVC", "GenuineTicket", "GenuineTicket.xml");

                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(ticketPath)!);
                    genuineXml.Save(ticketPath);
                }
                else
                {
                    throw new Exception("Matching activation ticket content was not found in the XML.");
                }

                await Task.Delay(3000);

                await CommandExecutor.InvokeRunCommand("clipup -v -o", true);
                await CommandExecutor.InvokeRunCommand("/c slmgr.vbs //b /ato");

                RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\International\Geo", "Name", originalGeo, RegistryValueKind.String);

                LicenseStatus();

                await Task.Delay(2000);

                if (IsWindowsActivated)
                {
                    //overlayWindow.Close();
                    // NotificationManager.Show("warn", "success_activate_noty").WithDelay(300).Restart();
                }
                else
                {
                    await CommandExecutor.InvokeRunCommand($"/c slmgr.vbs //b /ipk {keysKMS}");
                    await CommandExecutor.InvokeRunCommand("/c slmgr.vbs //b /skms kms.digiboy.ir");
                    await CommandExecutor.InvokeRunCommand("/c slmgr.vbs //b /ato");

                    LicenseStatus();

                    await Task.Delay(2000);

                    //overlayWindow.Close();

                    ///NotificationManager.Show("warn", IsWindowsActivated ? "success_activate_noty" : "error_activate_noty").WithDelay(300).Perform(IsWindowsActivated ? NotificationManager.NoticeAction.Restart : default);
                }
            }
            catch
            {
                //overlayWindow.Close();
                //NotificationManager.Show("warn", "error_activate_noty").WithDelay(300).Perform();
            }
        }
    }
}
