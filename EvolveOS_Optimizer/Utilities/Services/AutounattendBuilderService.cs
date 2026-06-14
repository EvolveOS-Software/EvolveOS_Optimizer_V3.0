// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Text;
using EvolveOS_Optimizer.Utilities.WinBuilder;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class AutounattendBuilderService
    {
        public async Task GenerateAsync(IsoBuildOptions options)
        {
            Debug.WriteLine($"[Builder] Starting XML Gen. Apps: {options.AppsToRemove?.Count ?? 0}, Tweaks: {options.RegistryTweaks?.Count ?? 0}, Elements: {options.ElementsToRemove?.Count ?? 0}");
            string xmlPath = options.OutputIsoPath;

            string psScript = GeneratePowerShellScript(options);
            string base64Script = Convert.ToBase64String(Encoding.Unicode.GetBytes(psScript));
            string xmlContent = GenerateXmlContent(options, base64Script);

            var utf8WithoutBom = new UTF8Encoding(false);
            await File.WriteAllTextAsync(xmlPath, xmlContent, utf8WithoutBom);
        }

        private string GeneratePowerShellScript(IsoBuildOptions options)
        {
            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Continue'");
            sb.AppendLine("Start-Transcript -Path \"$env:PUBLIC\\Desktop\\EvolveOS_Setup.log\" -Force");

            // Edge Removal (Matched to ISO Builder aggressiveness)
            if (options.RemoveMicrosoftEdge)
            {
                sb.AppendLine("Write-Output 'Nuking Microsoft Edge Ecosystem...'");
                sb.AppendLine("$edgePaths = @('C:\\Program Files (x86)\\Microsoft\\Edge', 'C:\\Program Files (x86)\\Microsoft\\EdgeUpdate', 'C:\\Program Files (x86)\\Microsoft\\EdgeCore')");
                sb.AppendLine("foreach ($ep in $edgePaths) { if (Test-Path $ep) { Remove-Item -Path $ep -Recurse -Force -ErrorAction SilentlyContinue } }");

                // Aggressive Registry Wipes (Translated for Live OS)
                sb.AppendLine(@"reg.exe delete ""HKLM\SYSTEM\CurrentControlSet\Services\edgeupdate"" /f 2>&1 | Out-Null");
                sb.AppendLine(@"reg.exe delete ""HKLM\SYSTEM\CurrentControlSet\Services\edgeupdatem"" /f 2>&1 | Out-Null");
                sb.AppendLine(@"reg.exe delete ""HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"" /f 2>&1 | Out-Null");
                sb.AppendLine(@"reg.exe delete ""HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge Update"" /f 2>&1 | Out-Null");
                sb.AppendLine(@"reg.exe delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"" /f 2>&1 | Out-Null");
            }

            // OneDrive Removal (Matched to ISO Builder aggressiveness)
            if (options.RemoveOneDrive)
            {
                sb.AppendLine("Write-Output 'Removing OneDrive...'");
                sb.AppendLine("Get-Process -Name 'OneDrive' -ErrorAction SilentlyContinue | Stop-Process -Force");
                sb.AppendLine("Start-Process -FilePath 'C:\\Windows\\System32\\OneDriveSetup.exe' -ArgumentList '/uninstall' -Wait -NoNewWindow");
                sb.AppendLine(@"reg.exe delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" /v OneDriveSetup /f 2>&1 | Out-Null");
            }

            // .NET 3.5 Enablement (Added back!)
            if (options.EnableNet35)
            {
                sb.AppendLine("Write-Output 'Enabling .NET Framework 3.5...'");
                // Because this is running live, we use the -Online flag instead of pointing to an offline mount directory
                sb.AppendLine("try { Enable-WindowsOptionalFeature -Online -FeatureName NetFx3 -All -ErrorAction Stop | Out-Null } catch { Write-Warning 'Failed to enable .NET 3.5' }");
            }

            // App Removals
            if (options.AppsToRemove != null)
            {
                sb.AppendLine("Write-Output 'Removing Selected Provisioned Apps...'");
                foreach (var app in options.AppsToRemove)
                {
                    sb.AppendLine($"try {{ Get-AppxPackage -AllUsers *{app}* | Remove-AppxPackage -AllUsers -ErrorAction Stop }} catch {{ }}");
                    sb.AppendLine($"try {{ Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -like '*{app}*' -or $_.PackageName -like '*{app}*' }} | Remove-AppxProvisionedPackage -Online -ErrorAction Stop }} catch {{ }}");
                }
            }

            // Optional Features
            if (options.ElementsToRemove != null && options.ElementsToRemove.Any())
            {
                sb.AppendLine("Write-Output 'Stripping Selected Windows Features...'");
                foreach (var pkg in options.ElementsToRemove)
                {
                    if (pkg.Contains("~~~~"))
                        sb.AppendLine($"try {{ Remove-WindowsCapability -Online -Name '{pkg}' -ErrorAction Stop }} catch {{ }}");
                    else
                        sb.AppendLine($"try {{ Disable-WindowsOptionalFeature -Online -FeatureName '{pkg}' -Remove -NoRestart -ErrorAction Stop }} catch {{ }}");
                }
            }

            if (options.RegistryTweaks != null && options.RegistryTweaks.Any())
            {
                sb.AppendLine("Write-Output 'Applying Tweaks...'");
                int regCounter = 0;

                foreach (var tweak in options.RegistryTweaks)
                {
                    if (string.IsNullOrWhiteSpace(tweak.RegCommand)) continue;
                    string cmd = tweak.RegCommand.Trim();

                    if (cmd.StartsWith("Windows Registry Editor", StringComparison.OrdinalIgnoreCase) ||
                        cmd.StartsWith("[") ||
                        cmd.StartsWith("[-"))
                    {
                        regCounter++;
                        sb.AppendLine($"$regContent{regCounter} = @\"");

                        if (!cmd.StartsWith("Windows Registry Editor"))
                            sb.AppendLine("Windows Registry Editor Version 5.00\n");

                        sb.AppendLine(cmd);
                        sb.AppendLine("\"@");
                        sb.AppendLine($"Set-Content -Path \"$env:TEMP\\tweak{regCounter}.reg\" -Value $regContent{regCounter} -Encoding UTF8");
                        sb.AppendLine($"Start-Process -FilePath \"reg.exe\" -ArgumentList \"import `\"$env:TEMP\\tweak{regCounter}.reg`\"\" -Wait -NoNewWindow");
                    }
                    else
                    {
                        sb.AppendLine(cmd);
                    }
                }
            }

            // UI Tweaks
            if (options.AlignTaskbarLeft)
                sb.AppendLine("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarAl /t REG_DWORD /d 0 /f");

            if (options.ForceDarkMode)
            {
                sb.AppendLine("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\" /v AppsUseLightTheme /t REG_DWORD /d 0 /f");
                sb.AppendLine("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\" /v SystemUsesLightTheme /t REG_DWORD /d 0 /f");
            }

            sb.AppendLine("Stop-Transcript");
            return sb.ToString();
        }

        private string GenerateXmlContent(IsoBuildOptions options, string base64Script)
        {
            var xmlSb = new StringBuilder();
            xmlSb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xmlSb.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\" xmlns:wcm=\"http://schemas-microsoft.com/WMIConfig/2002/State\">");

            xmlSb.AppendLine("  <settings pass=\"windowsPE\">");
            xmlSb.AppendLine("    <component name=\"Microsoft-Windows-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
            xmlSb.AppendLine("      <UserData><AcceptEula>true</AcceptEula></UserData>");

            if (options.BypassWin11Requirements)
            {
                xmlSb.AppendLine("      <RunSynchronous>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>1</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassTPMCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>2</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassSecureBootCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>3</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassStorageCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>4</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassCPUCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>5</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassRAMCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("      </RunSynchronous>");
            }
            xmlSb.AppendLine("    </component>");
            xmlSb.AppendLine("  </settings>");

            xmlSb.AppendLine("  <settings pass=\"oobeSystem\">");
            xmlSb.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
            xmlSb.AppendLine("      <UserAccounts>");
            xmlSb.AppendLine("        <LocalAccounts>");
            xmlSb.AppendLine("          <LocalAccount wcm:action=\"add\">");
            xmlSb.AppendLine("            <Password><Value></Value><PlainText>true</PlainText></Password>");
            xmlSb.AppendLine("            <DisplayName>Admin</DisplayName>");
            xmlSb.AppendLine("            <Group>Administrators</Group>");
            xmlSb.AppendLine("            <Name>Admin</Name>");
            xmlSb.AppendLine("          </LocalAccount>");
            xmlSb.AppendLine("        </LocalAccounts>");
            xmlSb.AppendLine("      </UserAccounts>");
            xmlSb.AppendLine("      <AutoLogon>");
            xmlSb.AppendLine("        <Password><Value></Value><PlainText>true</PlainText></Password>");
            xmlSb.AppendLine("        <Enabled>true</Enabled>");
            xmlSb.AppendLine("        <LogonCount>2</LogonCount>");
            xmlSb.AppendLine("        <Username>Admin</Username>");
            xmlSb.AppendLine("      </AutoLogon>");
            xmlSb.AppendLine("      <FirstLogonCommands>");
            xmlSb.AppendLine("        <SynchronousCommand wcm:action=\"add\">");
            xmlSb.AppendLine("          <Order>1</Order>");
            xmlSb.AppendLine($"          <CommandLine>cmd.exe /c start /wait powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand {base64Script}</CommandLine>");
            xmlSb.AppendLine("          <Description>Apply EvolveOS Tweaks</Description>");
            xmlSb.AppendLine("        </SynchronousCommand>");
            xmlSb.AppendLine("      </FirstLogonCommands>");
            xmlSb.AppendLine("    </component>");
            xmlSb.AppendLine("  </settings>");
            xmlSb.AppendLine("</unattend>");

            return xmlSb.ToString();
        }
    }
}