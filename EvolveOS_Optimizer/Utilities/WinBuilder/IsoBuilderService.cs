// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Utilities.WinBuilder
{
    public class IsoBuilderService
    {
        private static string RealBaseDir
        {
            get
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                return Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            }
        }

        public async Task BuildCustomIsoAsync(IsoBuildOptions options, IProgress<string> progress)
        {
            try
            {
                progress.Report("Preparing tools (oscdimg)...");
                string oscdimgPath = await EnsureOscdimgExtractAsync();

                progress.Report("Extracting original Windows ISO (This may take a few minutes)...");
                await ExtractIsoAsync(options.SourceIsoPath, options.WorkingDirectory, progress);

                progress.Report("Clearing ISO Read-Only attributes...");
                ClearReadOnlyAttributes(options.WorkingDirectory);

                progress.Report("Mounting and Servicing offline WIM Image...");
                await ApplyOfflineTweaksAsync(options, progress);

                progress.Report("Generating Unattended Setup (Hardware/Account Bypasses)...");
                GenerateUnattendXml(options);

                progress.Report("Repacking custom bootable ISO...");
                await RepackIsoAsync(oscdimgPath, options.WorkingDirectory, options.OutputIsoPath, progress);

                progress.Report("Cleaning up temporary files...");
                await Task.Delay(1000);
                ForceDeleteDirectory(options.WorkingDirectory);

                progress.Report("Success! Custom Offline-Serviced EvolveOS ISO created.");
            }
            catch (Exception ex)
            {
                throw new Exception($"ISO Build Failed: {ex.Message}", ex);
            }
        }

        private void ForceDeleteDirectory(string targetDir)
        {
            var dirInfo = new DirectoryInfo(targetDir);
            if (dirInfo.Exists)
            {
                foreach (var info in dirInfo.GetFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    info.Attributes = FileAttributes.Normal;
                }
                dirInfo.Delete(true);
            }
        }

        private void ClearReadOnlyAttributes(string directory)
        {
            var dirInfo = new DirectoryInfo(directory);
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                if (file.IsReadOnly)
                {
                    file.IsReadOnly = false;
                }
            }
        }

        private async Task<string> EnsureOscdimgExtractAsync()
        {
            string toolsDir = Path.Combine(RealBaseDir, "Tools");
            Directory.CreateDirectory(toolsDir);

            string exePath = Path.Combine(toolsDir, "oscdimg.exe");
            if (File.Exists(exePath)) return exePath;

            await Task.Run(() =>
            {
                byte[] resourceBytes = ArchiveManager.GetResourceBytes("oscdimg.exe.gz");
                if (resourceBytes == null || resourceBytes.Length == 0)
                {
                    throw new FileNotFoundException("Could not find oscdimg.exe.gz in the embedded resources!");
                }
                ArchiveManager.Unarchive(exePath, resourceBytes);
            });

            return exePath;
        }

        private async Task ExtractIsoAsync(string isoPath, string extractToPath, IProgress<string> progress)
        {
            Directory.CreateDirectory(extractToPath);
            string psScript = $@"
                $ErrorActionPreference = 'Stop'
                Write-Output 'Mounting Windows ISO...'
                $mountResult = Mount-DiskImage -ImagePath '{isoPath}' -PassThru
                $driveLetter = ($mountResult | Get-Volume).DriveLetter
                if (-not $driveLetter) {{ throw 'Failed to mount ISO.' }}
                
                Write-Output 'Copying installation files to workspace...'
                Copy-Item -Path ""$($driveLetter):\*"" -Destination '{extractToPath}' -Recurse -Force
                
                Write-Output 'Dismounting original ISO...'
                Dismount-DiskImage -ImagePath '{isoPath}'
            ";

            string tempScriptPath = Path.Combine(Path.GetTempPath(), "evolveos_extract.ps1");
            File.WriteAllText(tempScriptPath, psScript);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        progress.Report($"Extracting: {e.Data}");
                };
                process.BeginOutputReadLine();

                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0) throw new Exception($"Failed to extract ISO. Error: {error}");
            }
            File.Delete(tempScriptPath);
        }

        private async Task ApplyOfflineTweaksAsync(IsoBuildOptions options, IProgress<string> progress)
        {
            string mountDir = Path.Combine(options.WorkingDirectory, "mount");
            Directory.CreateDirectory(mountDir);

            string targetEdition = options.GetType().GetProperty("TargetEdition")?.GetValue(options, null)?.ToString() ?? "Pro";

            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Continue'");
            sb.AppendLine("$ProgressPreference = 'SilentlyContinue'");

            sb.AppendLine($"$workDir = '{options.WorkingDirectory}'");
            sb.AppendLine($"$mountDir = '{mountDir}'");
            sb.AppendLine("$esdPath = Join-Path $workDir 'sources\\install.esd'");
            sb.AppendLine("$wimPath = Join-Path $workDir 'sources\\install.wim'");
            sb.AppendLine("$newWimPath = Join-Path $workDir 'sources\\install_temp.wim'");

            sb.AppendLine("if (Test-Path $esdPath) {");
            sb.AppendLine("    Write-Output 'Converting install.esd to install.wim (This will take a few minutes)...'");
            sb.AppendLine("    dism.exe /Export-Image /SourceImageFile:$esdPath /SourceIndex:1 /DestinationImageFile:$wimPath /Compress:max /CheckIntegrity | Out-Null");
            sb.AppendLine("    Remove-Item $esdPath -Force");
            sb.AppendLine("}");

            sb.AppendLine($"$targetEdition = '{targetEdition}'");
            sb.AppendLine("$images = Get-WindowsImage -ImagePath $wimPath");
            sb.AppendLine("$selectedImage = $images | Where-Object ImageName -match $targetEdition");
            sb.AppendLine("$targetIndex = if ($selectedImage) { $selectedImage[0].ImageIndex } else { 1 }");

            sb.AppendLine("Write-Output \"Exporting Edition Index $targetIndex to new WIM...\"");
            sb.AppendLine("dism.exe /Export-Image /SourceImageFile:$wimPath /SourceIndex:$targetIndex /DestinationImageFile:$newWimPath /Compress:max /CheckIntegrity | Out-Null");
            sb.AppendLine("Remove-Item $wimPath -Force");
            sb.AppendLine("Rename-Item $newWimPath 'install.wim'");

            sb.AppendLine("Write-Output 'Mounting WIM to offline directory...'");
            sb.AppendLine("dism.exe /Mount-Image /ImageFile:$wimPath /Index:1 /MountDir:$mountDir | Out-Null");

            sb.AppendLine("try {");

            if (options.RemoveMicrosoftEdge)
            {
                sb.AppendLine(@"    Write-Output 'Nuking Microsoft Edge Ecosystem...'");
                sb.AppendLine(@"    $edgePaths = @(");
                sb.AppendLine(@"        (Join-Path $mountDir 'Program Files (x86)\Microsoft\Edge'),");
                sb.AppendLine(@"        (Join-Path $mountDir 'Program Files (x86)\Microsoft\EdgeUpdate'),");
                sb.AppendLine(@"        (Join-Path $mountDir 'Program Files (x86)\Microsoft\EdgeCore')");
                sb.AppendLine(@"    )");
                sb.AppendLine(@"    foreach ($ep in $edgePaths) { if (Test-Path $ep) { Remove-Item -Path $ep -Recurse -Force -ErrorAction SilentlyContinue } }");
            }

            if (options.RemoveOneDrive)
            {
                sb.AppendLine(@"    Write-Output 'Removing OneDrive...'");
                sb.AppendLine(@"    $odPath1 = Join-Path $mountDir 'Windows\System32\OneDriveSetup.exe'");
                sb.AppendLine(@"    $odPath2 = Join-Path $mountDir 'Windows\SysWOW64\OneDriveSetup.exe'");
                sb.AppendLine(@"    if (Test-Path $odPath1) { Remove-Item -Path $odPath1 -Force -ErrorAction SilentlyContinue }");
                sb.AppendLine(@"    if (Test-Path $odPath2) { Remove-Item -Path $odPath2 -Force -ErrorAction SilentlyContinue }");
            }

            sb.AppendLine("    Write-Output 'Removing Selected Provisioned Apps...'");
            foreach (var app in options.AppsToRemove)
            {
                sb.AppendLine($"    Get-AppxProvisionedPackage -Path $mountDir | Where-Object {{ $_.DisplayName -match '{app}' -or $_.PackageName -match '{app}' }} | ForEach-Object {{ Remove-AppxProvisionedPackage -Path $mountDir -PackageName $_.PackageName | Out-Null }}");
            }

            sb.AppendLine("    Write-Output 'Loading Offline Registries...'");
            sb.AppendLine(@"    reg.exe load HKLM\OffSoft ""$mountDir\Windows\System32\config\SOFTWARE"" 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe load HKLM\OffSys ""$mountDir\Windows\System32\config\SYSTEM"" 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe load HKLM\OffDef ""$mountDir\Users\Default\NTUSER.DAT"" 2>&1 | Out-Null");

            if (options.RemoveMicrosoftEdge)
            {
                sb.AppendLine(@"    reg.exe delete ""HKLM\OffSys\ControlSet001\Services\edgeupdate"" /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe delete ""HKLM\OffSys\ControlSet001\Services\edgeupdatem"" /f 2>&1 | Out-Null");
            }

            if (options.RemoveOneDrive)
            {
                sb.AppendLine(@"    reg.exe delete ""HKLM\OffDef\Software\Microsoft\Windows\CurrentVersion\Run"" /v OneDriveSetup /f 2>&1 | Out-Null");
            }

            // ---> THE FIX: Extreme Cloud Content Blocking to Destroy Ghost Stub Apps <---
            sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Microsoft\Windows\CurrentVersion\OOBE"" /v DisableZDP /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Policies\Microsoft\Windows\CloudContent"" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Policies\Microsoft\Windows\CloudContent"" /v DisableCloudOptimizedContent /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Policies\Microsoft\Windows\CloudContent"" /v DisableConsumerAccountStateContent /t REG_DWORD /d 1 /f 2>&1 | Out-Null");

            sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Policies\Microsoft\Windows\CloudContent"" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Policies\Microsoft\Windows\CloudContent"" /v DisableCloudOptimizedContent /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Policies\Microsoft\Windows\CloudContent"" /v DisableConsumerAccountStateContent /t REG_DWORD /d 1 /f 2>&1 | Out-Null");

            sb.AppendLine("    Write-Output 'Applying Service Tweaks...'");
            foreach (var tweak in options.ServiceTweaks)
            {
                string startValue = tweak.StartupType.ToLower() switch { "disabled" => "4", "manual" => "3", "automatic" => "2", "automaticdelayedstart" => "2", _ => "3" };
                sb.AppendLine($"    reg.exe add \"HKLM\\OffSys\\ControlSet001\\Services\\{tweak.ServiceName}\" /v Start /t REG_DWORD /d {startValue} /f 2>&1 | Out-Null");
            }

            sb.AppendLine(@"    $perUserSvc = @('CDPUserSvc','OneSyncSvc','PimIndexMaintenanceSvc','UserDataSvc','UnistoreSvc','BcastDVRUserService','PrintWorkflowUserSvc','DevicePickerUserSvc','DevicesFlowUserSvc','ConsentUxUserSvc','CredentialEnrollmentManagerUserSvc','CaptureService','BluetoothUserService')");
            sb.AppendLine(@"    foreach ($svc in $perUserSvc) { reg.exe add ""HKLM\OffSys\ControlSet001\Services\$svc"" /v Start /t REG_DWORD /d 4 /f 2>&1 | Out-Null }");

            sb.AppendLine("    Write-Output 'Applying System UI Preferences...'");
            if (options.AlignTaskbarLeft)
            {
                sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"" /v TaskbarAl /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
            }

            if (options.ForceDarkMode)
            {
                sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v AppsUseLightTheme /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v SystemUsesLightTheme /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v AppsUseLightTheme /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v SystemUsesLightTheme /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Microsoft\Windows\CurrentVersion\Themes"" /v InstallTheme /t REG_EXPAND_SZ /d ""%SystemRoot%\resources\Themes\dark.theme"" /f 2>&1 | Out-Null");
            }

            sb.AppendLine("    Write-Output 'Applying Selected Registry Tweaks...'");
            foreach (var tweak in options.RegistryTweaks)
            {
                string offlineCmd = tweak.RegCommand
                    .Replace("HKLM\\SOFTWARE", "HKLM\\OffSoft", StringComparison.OrdinalIgnoreCase)
                    .Replace("HKLM\\SYSTEM\\CurrentControlSet", "HKLM\\OffSys\\ControlSet001", StringComparison.OrdinalIgnoreCase)
                    .Replace("HKCU", "HKLM\\OffDef", StringComparison.OrdinalIgnoreCase);

                sb.AppendLine($"    {offlineCmd} 2>&1 | Out-Null");
            }

            if (options.AppsToRemove.Any())
            {
                sb.AppendLine("    Write-Output 'Clearing Start Menu Ghost Pins...'");
                sb.AppendLine(@"    $layoutDir = ""$mountDir\Users\Default\AppData\Local\Microsoft\Windows\Shell""");
                sb.AppendLine(@"    if (-not (Test-Path $layoutDir)) { New-Item -Path $layoutDir -ItemType Directory -Force | Out-Null }");
                sb.AppendLine(@"    Remove-Item ""$layoutDir\DefaultLayouts.xml"" -Force -ErrorAction SilentlyContinue");
                sb.AppendLine(@"    Set-Content -Path ""$layoutDir\LayoutModification.json"" -Value '{""pinnedList"":[]}' -Force");
                sb.AppendLine(@"    $xmlLayout = '<LayoutModificationTemplate xmlns:defaultlayout=""http://schemas.microsoft.com/Start/2014/FullDefaultLayout"" xmlns:start=""http://schemas.microsoft.com/Start/2014/StartLayout"" Version=""1"" xmlns=""http://schemas.microsoft.com/Start/2014/LayoutModification""><LayoutOptions StartTileGroupCellWidth=""6"" /><DefaultLayoutOverride><StartLayoutCollection><defaultlayout:StartLayout GroupCellWidth=""6"" /></StartLayoutCollection></DefaultLayoutOverride></LayoutModificationTemplate>'");
                sb.AppendLine(@"    Set-Content -Path ""$layoutDir\LayoutModification.xml"" -Value $xmlLayout -Force");

                // Extra cleanup to ensure Default Start Menu bin doesn't force stubs
                sb.AppendLine(@"    Remove-Item ""$mountDir\Users\Default\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start.bin"" -Force -ErrorAction SilentlyContinue");
            }

            if (options.EnableNet35)
            {
                sb.AppendLine("    Write-Output 'Enabling .NET Framework 3.5...'");
                sb.AppendLine(@"    $sxsPath = Join-Path $workDir 'sources\sxs'");
                sb.AppendLine(@"    if (Test-Path ""$sxsPath\*.cab"") {");
                sb.AppendLine(@"        Enable-WindowsOptionalFeature -Path $mountDir -FeatureName NetFx3 -All -LimitAccess -Source $sxsPath | Out-Null");
                sb.AppendLine(@"    }");
            }

            sb.AppendLine("} finally {");
            sb.AppendLine("    [System.GC]::Collect(); [System.GC]::WaitForPendingFinalizers(); Start-Sleep -Seconds 2");
            sb.AppendLine(@"    reg.exe unload HKLM\OffSoft 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe unload HKLM\OffSys 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe unload HKLM\OffDef 2>&1 | Out-Null");

            sb.AppendLine("    Write-Output 'Saving and Dismounting WIM (This will take a few minutes)...'");
            sb.AppendLine("    dism.exe /Unmount-Image /MountDir:$mountDir /Commit | Out-Null");

            if (options.ImageFormat != null && options.ImageFormat.Equals("ESD", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("    Write-Output 'Compressing final image to ESD (This requires high CPU and takes time)...'");
                sb.AppendLine("    dism.exe /Export-Image /SourceImageFile:$wimPath /SourceIndex:1 /DestinationImageFile:$esdPath /Compress:recovery /CheckIntegrity | Out-Null");
                sb.AppendLine("    Remove-Item $wimPath -Force");
            }

            sb.AppendLine("}");

            string tempScriptPath = Path.Combine(Path.GetTempPath(), "evolveos_offline_service.ps1");
            File.WriteAllText(tempScriptPath, sb.ToString());

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        progress.Report($"Servicing: {e.Data}");
                };
                process.BeginOutputReadLine();

                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0) throw new Exception($"Failed to service offline WIM. Error: {error}");
            }
            File.Delete(tempScriptPath);
        }

        private void GenerateUnattendXml(IsoBuildOptions options)
        {
            string xmlPath = Path.Combine(options.WorkingDirectory, "autounattend.xml");
            var xmlSb = new StringBuilder();

            xmlSb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xmlSb.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\" xmlns:wcm=\"http://schemas-microsoft.com/WMIConfig/2002/State\">");

            xmlSb.AppendLine("  <settings pass=\"windowsPE\">");
            xmlSb.AppendLine("    <component name=\"Microsoft-Windows-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
            xmlSb.AppendLine("      <UserData><AcceptEula>true</AcceptEula></UserData>");
            xmlSb.AppendLine("      <DynamicUpdate>");
            xmlSb.AppendLine("        <Enable>false</Enable>");
            xmlSb.AppendLine("        <WillShowUI>OnError</WillShowUI>");
            xmlSb.AppendLine("      </DynamicUpdate>");

            if (options.BypassWin11Requirements)
            {
                xmlSb.AppendLine("      <RunSynchronous>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>1</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassTPMCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>2</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassSecureBootCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>3</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassStorageCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>4</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassCPUCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>5</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassRAMCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\"><Order>6</Order><Path>reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassDiskCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine("      </RunSynchronous>");
            }
            xmlSb.AppendLine("    </component>");
            xmlSb.AppendLine("  </settings>");

            xmlSb.AppendLine("  <settings pass=\"specialize\">");
            xmlSb.AppendLine("    <component name=\"Microsoft-Windows-Deployment\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
            xmlSb.AppendLine("      <RunSynchronous>");

            int specOrder = 1;
            if (options.BypassMicrosoftAccount)
            {
                xmlSb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{specOrder++}</Order><Description>BypassNRO</Description><Path>reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE\" /v BypassNRO /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{specOrder++}</Order><Description>Disable Network to force Local Account</Description><Path>powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Disable-NetAdapter -Confirm:$false\"</Path></RunSynchronousCommand>");
            }

            // ---> THE FIX: Removes the Edge Registry Uninstaller Keys natively in the Specialize pass <---
            if (options.RemoveMicrosoftEdge)
            {
                xmlSb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{specOrder++}</Order><Description>Remove Edge from Installed Apps (x64)</Description><Path>cmd /c reg.exe delete \"HKLM\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge\" /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{specOrder++}</Order><Description>Remove Edge Update from Installed Apps</Description><Path>cmd /c reg.exe delete \"HKLM\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge Update\" /f</Path></RunSynchronousCommand>");
                xmlSb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{specOrder++}</Order><Description>Remove Edge from Installed Apps (x86)</Description><Path>cmd /c reg.exe delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge\" /f</Path></RunSynchronousCommand>");
            }

            xmlSb.AppendLine("      </RunSynchronous>");
            xmlSb.AppendLine("    </component>");
            xmlSb.AppendLine("  </settings>");

            xmlSb.AppendLine("  <settings pass=\"oobeSystem\">");
            xmlSb.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
            xmlSb.AppendLine("      <OOBE>");
            xmlSb.AppendLine("        <HideEULAPage>true</HideEULAPage>");
            xmlSb.AppendLine("        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>");
            xmlSb.AppendLine("        <HideOnlineAccountScreens>true</HideOnlineAccountScreens>");
            xmlSb.AppendLine("        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>");
            xmlSb.AppendLine("        <ProtectYourPC>3</ProtectYourPC>");
            xmlSb.AppendLine("      </OOBE>");
            xmlSb.AppendLine("      <UserAccounts>");
            xmlSb.AppendLine("        <LocalAccounts>");
            xmlSb.AppendLine("          <LocalAccount wcm:action=\"add\">");
            xmlSb.AppendLine("            <Password><Value></Value><PlainText>true</PlainText></Password>");
            xmlSb.AppendLine("            <Description>Local Administrator</Description>");
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
            int logonOrder = 1;

            if (options.BypassMicrosoftAccount)
            {
                xmlSb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{logonOrder++}</Order><CommandLine>powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Enable-NetAdapter -Confirm:$false\"</CommandLine></SynchronousCommand>");
            }

            if (options.AppsToRemove.Any())
            {
                xmlSb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{logonOrder++}</Order><CommandLine>cmd /c reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f</CommandLine></SynchronousCommand>");

                foreach (var appPackage in options.AppsToRemove)
                {
                    xmlSb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{logonOrder++}</Order><CommandLine>powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -Command \"Get-AppxPackage -AllUsers *{appPackage}* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue\"</CommandLine></SynchronousCommand>");
                }
            }

            xmlSb.AppendLine("      </FirstLogonCommands>");
            xmlSb.AppendLine("    </component>");
            xmlSb.AppendLine("  </settings>");
            xmlSb.AppendLine("</unattend>");

            File.WriteAllText(xmlPath, xmlSb.ToString(), Encoding.UTF8);
        }

        private async Task RepackIsoAsync(string oscdimgPath, string workingDir, string outputIsoPath, IProgress<string> progress)
        {
            string etfsBoot = Path.Combine(workingDir, "boot", "etfsboot.com");
            string efiSys = Path.Combine(workingDir, "efi", "microsoft", "boot", "efisys.bin");

            if (!File.Exists(etfsBoot) || !File.Exists(efiSys))
                throw new Exception("Extracted ISO is missing boot files. Ensure you selected a valid Windows ISO.");

            string cleanWorkingDir = workingDir.TrimEnd('\\');
            string arguments = $"-m -o -u2 -udfver102 -bootdata:2#p0,e,b\"{etfsBoot}\"#pEF,e,b\"{efiSys}\" \"{cleanWorkingDir}\" \"{outputIsoPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = oscdimgPath,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        progress.Report($"Repacking: {e.Data}");
                };
                process.BeginOutputReadLine();

                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0) throw new Exception($"oscdimg failed to pack the ISO. Error: {error}");
            }
        }
    }
}