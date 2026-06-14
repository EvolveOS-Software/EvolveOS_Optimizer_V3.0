// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using System.Threading;
using EvolveOS_Optimizer.Utilities.Helpers;
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

        public async Task BuildCustomIsoAsync(IsoBuildOptions options, IProgress<string> progress, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress.Report(ResourceString.GetString("isobuilder_bg_prep_tools") ?? "Preparing tools (oscdimg)...");
                string oscdimgPath = await EnsureOscdimgExtractAsync();

                cancellationToken.ThrowIfCancellationRequested();

                progress.Report(ResourceString.GetString("isobuilder_bg_extract_iso") ?? "Extracting original Windows ISO (This may take a few minutes)...");
                await ExtractIsoAsync(options.SourceIsoPath, options.WorkingDirectory, progress, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                progress.Report(ResourceString.GetString("isobuilder_bg_clear_readonly") ?? "Clearing ISO Read-Only attributes...");
                ClearReadOnlyAttributes(options.WorkingDirectory);

                cancellationToken.ThrowIfCancellationRequested();

                progress.Report(ResourceString.GetString("isobuilder_bg_mount_wim") ?? "Mounting and Servicing offline WIM Image...");
                await ApplyOfflineTweaksAsync(options, progress, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                progress.Report(ResourceString.GetString("isobuilder_bg_gen_unattend") ?? "Generating Unattended Setup (Hardware/Account Bypasses)...");
                GenerateUnattendXml(options);

                cancellationToken.ThrowIfCancellationRequested();

                if (!options.EnableNet35)
                {
                    progress.Report(ResourceString.GetString("isobuilder_bg_purge_net35") ?? "Purging legacy .NET 3.5 payloads from ISO...");
                    string sxsPath = Path.Combine(options.WorkingDirectory, "sources", "sxs");
                    if (Directory.Exists(sxsPath))
                    {
                        ForceDeleteDirectory(sxsPath);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                progress.Report(ResourceString.GetString("isobuilder_bg_repack_iso") ?? "Repacking custom bootable ISO...");
                await RepackIsoAsync(oscdimgPath, options.WorkingDirectory, options.OutputIsoPath, progress, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                progress.Report(ResourceString.GetString("isobuilder_bg_cleanup") ?? "Cleaning up temporary files...");
                await Task.Delay(1000, cancellationToken);
                ForceDeleteDirectory(options.WorkingDirectory);

                progress.Report(ResourceString.GetString("isobuilder_bg_success") ?? "Success! Custom Offline-Serviced EvolveOS ISO created.");
            }
            catch (OperationCanceledException)
            {
                try
                {
                    string mountDir = Path.Combine(options.WorkingDirectory, "mount");
                    if (Directory.Exists(mountDir))
                    {
                        Process.Start(new ProcessStartInfo("dism.exe", $"/Unmount-Image /MountDir:\"{mountDir}\" /Discard") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit();
                        Process.Start(new ProcessStartInfo("dism.exe", "/Cleanup-Wim") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit();
                    }
                    ForceDeleteDirectory(options.WorkingDirectory);
                }
                catch { }

                throw;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
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
                    string errMissing = ResourceString.GetString("isobuilder_bg_err_oscdimg_missing") ?? "Could not find oscdimg.exe.gz in the embedded resources!";
                    throw new FileNotFoundException(errMissing);
                }
                ArchiveManager.Unarchive(exePath, resourceBytes);
            });

            return exePath;
        }

        private async Task ExtractIsoAsync(string isoPath, string extractToPath, IProgress<string> progress, CancellationToken cancellationToken)
        {
            string errPsMount = ResourceString.GetString("isobuilder_err_ps_mount_failed") ?? "Failed to mount ISO.";
            string msgMount = ResourceString.GetString("isobuilder_ps_mount_iso") ?? "Mounting Windows ISO...";
            string msgCopy = ResourceString.GetString("isobuilder_ps_copy_files") ?? "Copying installation files to workspace...";
            string msgDismount = ResourceString.GetString("isobuilder_ps_dismount_iso") ?? "Dismounting original ISO...";

            Directory.CreateDirectory(extractToPath);
            string psScript = $@"
                $ErrorActionPreference = 'Stop'
                Write-Output '{msgMount}'
                $mountResult = Mount-DiskImage -ImagePath '{isoPath}' -PassThru
                $driveLetter = ($mountResult | Get-Volume).DriveLetter
                if (-not $driveLetter) {{ throw '{errPsMount}' }}
                
                Write-Output '{msgCopy}'
                Copy-Item -Path ""$($driveLetter):\*"" -Destination '{extractToPath}' -Recurse -Force
                
                Write-Output '{msgDismount}'
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
                        progress.Report(e.Data);
                };
                process.BeginOutputReadLine();

                using var ctr = cancellationToken.Register(() => { try { process.Kill(true); } catch { } });

                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                string error = await errorTask;

                if (process.ExitCode != 0)
                {
                    string errExtract = ResourceString.GetString("isobuilder_bg_err_extract") ?? "Failed to extract ISO. Error:";
                    throw new Exception($"{errExtract} {error}");
                }
            }
            File.Delete(tempScriptPath);
        }

        private async Task ApplyOfflineTweaksAsync(IsoBuildOptions options, IProgress<string> progress, CancellationToken cancellationToken)
        {
            string mountDir = Path.Combine(options.WorkingDirectory, "mount");
            Directory.CreateDirectory(mountDir);

            string targetEdition = options.GetType().GetProperty("TargetEdition")?.GetValue(options, null)?.ToString() ?? "Pro";

            string errEsd = ResourceString.GetString("isobuilder_err_ps_esd_failed") ?? "Critical Error: DISM failed to create the install.esd file.";
            string errWim = ResourceString.GetString("isobuilder_err_ps_wim_failed") ?? "Critical Error: DISM failed to create the optimized install.wim file.";

            string msgExcludeDef = ResourceString.GetString("isobuilder_ps_exclude_defender") ?? "Temporarily excluding workspace from Windows Defender locks...";
            string msgConvertEsd = ResourceString.GetString("isobuilder_ps_convert_esd") ?? "Converting install.esd to install.wim (This will take a few minutes)...";
            string msgExportEd = ResourceString.GetString("isobuilder_ps_export_edition") ?? "Exporting selected Edition Index to new WIM...";
            string msgMountOff = ResourceString.GetString("isobuilder_ps_mount_offline") ?? "Mounting WIM to offline directory...";
            string msgNukeEdge = ResourceString.GetString("isobuilder_ps_nuke_edge") ?? "Nuking Microsoft Edge Ecosystem...";
            string msgRemOneDrive = ResourceString.GetString("isobuilder_ps_remove_onedrive") ?? "Removing OneDrive...";
            string msgRemApps = ResourceString.GetString("isobuilder_ps_remove_apps") ?? "Removing Selected Provisioned Apps...";
            string msgStripFeat = ResourceString.GetString("isobuilder_ps_strip_features") ?? "Stripping Selected Windows Features & Capabilities...";
            string msgLoadReg = ResourceString.GetString("isobuilder_ps_load_registry") ?? "Loading Offline Registries...";
            string msgApplySvc = ResourceString.GetString("isobuilder_ps_apply_services") ?? "Applying Service Tweaks...";
            string msgApplyUI = ResourceString.GetString("isobuilder_ps_apply_ui") ?? "Applying System UI Preferences...";
            string msgApplyReg = ResourceString.GetString("isobuilder_ps_apply_registry") ?? "Applying Selected Registry Tweaks...";
            string msgClearStart = ResourceString.GetString("isobuilder_ps_clear_start") ?? "Clearing Start Menu Ghost Pins...";
            string msgEnableNet35 = ResourceString.GetString("isobuilder_ps_enable_net35") ?? "Enabling .NET Framework 3.5...";
            string msgWipeWinRe = ResourceString.GetString("isobuilder_ps_wipe_winre") ?? "Executing Clean Wipe of WinRE payload...";
            string msgStripProtect = ResourceString.GetString("isobuilder_ps_strip_protect") ?? "Stripping protections from";
            string msgAttDelete = ResourceString.GetString("isobuilder_ps_attempt_delete") ?? "Attempting deletion...";
            string msgGenDummy = ResourceString.GetString("isobuilder_ps_gen_dummy_wim") ?? "Generating valid 2KB dummy WIM to satisfy Windows Setup...";
            string msgDeepClean = ResourceString.GetString("isobuilder_ps_deep_clean") ?? "Deep Cleaning Component Store (ResetBase) to shrink image size...";
            string msgPurgeCache = ResourceString.GetString("isobuilder_ps_purge_cache") ?? "Purging System Cache, Temp, and Log Files...";
            string msgSaveWim = ResourceString.GetString("isobuilder_ps_save_wim") ?? "Saving and Dismounting WIM (This will take a few minutes)...";
            string msgCompressEsd = ResourceString.GetString("isobuilder_ps_compress_esd") ?? "Compressing final image to ESD (This requires high CPU and takes time)...";
            string msgReclaimSpace = ResourceString.GetString("isobuilder_ps_reclaim_space") ?? "Exporting WIM to reclaim deleted space (This takes a few minutes)...";

            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Continue'");
            sb.AppendLine("$ProgressPreference = 'SilentlyContinue'");

            sb.AppendLine(@"Start-Transcript -Path ""$env:USERPROFILE\Desktop\EvolveOS_Debug.log"" -Force");

            sb.AppendLine($"$workDir = '{options.WorkingDirectory}'");

            sb.AppendLine($"Write-Output '{msgExcludeDef}'");
            sb.AppendLine(@"Add-MpPreference -ExclusionPath $workDir -ErrorAction SilentlyContinue");

            sb.AppendLine($"$mountDir = '{mountDir}'");
            sb.AppendLine("$esdPath = Join-Path $workDir 'sources\\install.esd'");
            sb.AppendLine("$wimPath = Join-Path $workDir 'sources\\install.wim'");
            sb.AppendLine("$newWimPath = Join-Path $workDir 'sources\\install_temp.wim'");

            sb.AppendLine("if (Test-Path $esdPath) {");
            sb.AppendLine($"    Write-Output '{msgConvertEsd}'");
            sb.AppendLine("    dism.exe /Export-Image /SourceImageFile:$esdPath /SourceIndex:1 /DestinationImageFile:$wimPath /Compress:max /CheckIntegrity | Out-Null");
            sb.AppendLine("    Remove-Item $esdPath -Force");
            sb.AppendLine("}");

            sb.AppendLine($"$targetEdition = '{targetEdition}'");
            sb.AppendLine("$images = Get-WindowsImage -ImagePath $wimPath");
            sb.AppendLine("$selectedImage = $images | Where-Object ImageName -match $targetEdition");
            sb.AppendLine("$targetIndex = if ($selectedImage) { $selectedImage[0].ImageIndex } else { 1 }");

            sb.AppendLine($"Write-Output '{msgExportEd}'");
            sb.AppendLine("dism.exe /Export-Image /SourceImageFile:$wimPath /SourceIndex:$targetIndex /DestinationImageFile:$newWimPath /Compress:max /CheckIntegrity | Out-Null");
            sb.AppendLine("Remove-Item $wimPath -Force");
            sb.AppendLine("Rename-Item $newWimPath 'install.wim'");

            sb.AppendLine($"Write-Output '{msgMountOff}'");
            sb.AppendLine("dism.exe /Mount-Image /ImageFile:$wimPath /Index:1 /MountDir:$mountDir | Out-Null");

            sb.AppendLine("try {");

            if (options.RemoveMicrosoftEdge)
            {
                sb.AppendLine($"    Write-Output '{msgNukeEdge}'");
                sb.AppendLine(@"    $edgePaths = @(");
                sb.AppendLine(@"        (Join-Path $mountDir 'Program Files (x86)\Microsoft\Edge'),");
                sb.AppendLine(@"        (Join-Path $mountDir 'Program Files (x86)\Microsoft\EdgeUpdate'),");
                sb.AppendLine(@"        (Join-Path $mountDir 'Program Files (x86)\Microsoft\EdgeCore')");
                sb.AppendLine(@"    )");
                sb.AppendLine(@"    foreach ($ep in $edgePaths) { if (Test-Path $ep) { Remove-Item -Path $ep -Recurse -Force -ErrorAction SilentlyContinue } }");
            }

            if (options.RemoveOneDrive)
            {
                sb.AppendLine($"    Write-Output '{msgRemOneDrive}'");
                sb.AppendLine(@"    $odPath1 = Join-Path $mountDir 'Windows\System32\OneDriveSetup.exe'");
                sb.AppendLine(@"    $odPath2 = Join-Path $mountDir 'Windows\SysWOW64\OneDriveSetup.exe'");
                sb.AppendLine(@"    if (Test-Path $odPath1) { Remove-Item -Path $odPath1 -Force -ErrorAction SilentlyContinue }");
                sb.AppendLine(@"    if (Test-Path $odPath2) { Remove-Item -Path $odPath2 -Force -ErrorAction SilentlyContinue }");
            }

            if (options.AppsToRemove != null && options.AppsToRemove.Any())
            {
                sb.AppendLine($"    Write-Output '{msgRemApps}'");
                foreach (var app in options.AppsToRemove)
                {
                    sb.AppendLine($"    Get-AppxProvisionedPackage -Path $mountDir | Where-Object {{ $_.DisplayName -match '{app}' -or $_.PackageName -match '{app}' }} | ForEach-Object {{ Remove-AppxProvisionedPackage -Path $mountDir -PackageName $_.PackageName | Out-Null }}");
                }
            }

            if (options.ElementsToRemove != null && options.ElementsToRemove.Any())
            {
                sb.AppendLine($"    Write-Output '{msgStripFeat}'");
                foreach (var pkg in options.ElementsToRemove)
                {
                    sb.AppendLine("    try {");
                    if (pkg.Contains("~~~~"))
                    {
                        sb.AppendLine($"        Remove-WindowsCapability -Path $mountDir -Name '{pkg}' -ErrorAction Stop | Out-Null");
                    }
                    else
                    {
                        sb.AppendLine($"        Disable-WindowsOptionalFeature -Path $mountDir -FeatureName '{pkg}' -Remove -NoRestart -ErrorAction Stop | Out-Null");
                    }
                    sb.AppendLine($"    }} catch {{ }}");
                }
            }

            sb.AppendLine($"    Write-Output '{msgLoadReg}'");
            sb.AppendLine(@"    reg.exe load HKLM\OffSoft ""$mountDir\Windows\System32\config\SOFTWARE"" 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe load HKLM\OffSys ""$mountDir\Windows\System32\config\SYSTEM"" 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe load HKLM\OffDef ""$mountDir\Users\Default\NTUSER.DAT"" 2>&1 | Out-Null");

            if (options.RemoveMicrosoftEdge)
            {
                sb.AppendLine(@"    reg.exe delete ""HKLM\OffSys\ControlSet001\Services\edgeupdate"" /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe delete ""HKLM\OffSys\ControlSet001\Services\edgeupdatem"" /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe delete ""HKLM\OffSoft\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"" /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe delete ""HKLM\OffSoft\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge Update"" /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe delete ""HKLM\OffSoft\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"" /f 2>&1 | Out-Null");
            }

            if (options.RemoveOneDrive)
            {
                sb.AppendLine(@"    reg.exe delete ""HKLM\OffDef\Software\Microsoft\Windows\CurrentVersion\Run"" /v OneDriveSetup /f 2>&1 | Out-Null");
            }

            sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Microsoft\Windows\CurrentVersion\OOBE"" /v DisableZDP /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Policies\Microsoft\Windows\CloudContent"" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Policies\Microsoft\Windows\CloudContent"" /v DisableCloudOptimizedContent /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Policies\Microsoft\Windows\CloudContent"" /v DisableConsumerAccountStateContent /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Policies\Microsoft\Windows\CloudContent"" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Policies\Microsoft\Windows\CloudContent"" /v DisableCloudOptimizedContent /t REG_DWORD /d 1 /f 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Policies\Microsoft\Windows\CloudContent"" /v DisableConsumerAccountStateContent /t REG_DWORD /d 1 /f 2>&1 | Out-Null");

            sb.AppendLine($"    Write-Output '{msgApplySvc}'");
            if (options.ServiceTweaks != null)
            {
                foreach (var tweak in options.ServiceTweaks)
                {
                    string startValue = tweak.StartupType?.ToLower() switch { "disabled" => "4", "manual" => "3", "automatic" => "2", "automaticdelayedstart" => "2", _ => "3" };
                    sb.AppendLine($"    reg.exe add \"HKLM\\OffSys\\ControlSet001\\Services\\{tweak.ServiceName}\" /v Start /t REG_DWORD /d {startValue} /f 2>&1 | Out-Null");
                }
            }

            sb.AppendLine(@"    $perUserSvc = @('CDPUserSvc','OneSyncSvc','PimIndexMaintenanceSvc','UserDataSvc','UnistoreSvc','BcastDVRUserService','PrintWorkflowUserSvc','DevicePickerUserSvc','DevicesFlowUserSvc','ConsentUxUserSvc','CredentialEnrollmentManagerUserSvc','CaptureService','BluetoothUserService')");
            sb.AppendLine(@"    foreach ($svc in $perUserSvc) { reg.exe add ""HKLM\OffSys\ControlSet001\Services\$svc"" /v Start /t REG_DWORD /d 4 /f 2>&1 | Out-Null }");

            sb.AppendLine($"    Write-Output '{msgApplyUI}'");
            if (options.AlignTaskbarLeft)
            {
                sb.AppendLine(@"    reg.exe add ""HKLM\OffDef\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"" /v TaskbarAl /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
            }

            if (options.ForceDarkMode)
            {
                sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v AppsUseLightTheme /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v SystemUsesLightTheme /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
            }

            sb.AppendLine($"    Write-Output '{msgApplyReg}'");

            if (options.RegistryTweaks != null)
            {
                int regCounter = 0;
                foreach (var tweak in options.RegistryTweaks)
                {
                    if (string.IsNullOrWhiteSpace(tweak.RegCommand)) continue;

                    string offlineCmd = tweak.RegCommand
                        .Replace("HKLM\\SOFTWARE", "HKLM\\OffSoft", StringComparison.OrdinalIgnoreCase)
                        .Replace("HKLM\\SYSTEM\\CurrentControlSet", "HKLM\\OffSys\\ControlSet001", StringComparison.OrdinalIgnoreCase)
                        .Replace("HKCU", "HKLM\\OffDef", StringComparison.OrdinalIgnoreCase);

                    string trimmedCmd = offlineCmd.TrimStart();

                    if (trimmedCmd.StartsWith("Windows Registry Editor", StringComparison.OrdinalIgnoreCase) ||
                        trimmedCmd.StartsWith("[") ||
                        trimmedCmd.StartsWith("[-"))
                    {
                        regCounter++;
                        sb.AppendLine($"    $regContent{regCounter} = @\"");
                        if (!trimmedCmd.StartsWith("Windows Registry Editor"))
                            sb.AppendLine("Windows Registry Editor Version 5.00\n");
                        sb.AppendLine(offlineCmd);
                        sb.AppendLine("    \"@");
                        sb.AppendLine($"    Set-Content -Path \"$mountDir\\tweak{regCounter}.reg\" -Value $regContent{regCounter} -Encoding UTF8");
                        sb.AppendLine($"    reg.exe import \"$mountDir\\tweak{regCounter}.reg\" 2>&1 | Out-Null");
                        sb.AppendLine($"    Remove-Item \"$mountDir\\tweak{regCounter}.reg\" -Force -ErrorAction SilentlyContinue");
                    }
                    else
                    {
                        sb.AppendLine($"    {offlineCmd} 2>&1 | Out-Null");
                    }
                }
            }

            if (options.AppsToRemove != null && options.AppsToRemove.Any())
            {
                sb.AppendLine($"    Write-Output '{msgClearStart}'");
                sb.AppendLine(@"    $layoutDir = ""$mountDir\Users\Default\AppData\Local\Microsoft\Windows\Shell""");
                sb.AppendLine(@"    if (-not (Test-Path $layoutDir)) { New-Item -Path $layoutDir -ItemType Directory -Force | Out-Null }");
                sb.AppendLine(@"    Remove-Item ""$layoutDir\DefaultLayouts.xml"" -Force -ErrorAction SilentlyContinue");
                sb.AppendLine(@"    Set-Content -Path ""$layoutDir\LayoutModification.json"" -Value '{""pinnedList"":[]}' -Force");
                sb.AppendLine(@"    $xmlLayout = '<LayoutModificationTemplate xmlns:defaultlayout=""http://schemas.microsoft.com/Start/2014/FullDefaultLayout"" xmlns:start=""http://schemas.microsoft.com/Start/2014/StartLayout"" Version=""1"" xmlns=""http://schemas.microsoft.com/Start/2014/LayoutModification""><LayoutOptions StartTileGroupCellWidth=""6"" /><DefaultLayoutOverride><StartLayoutCollection><defaultlayout:StartLayout GroupCellWidth=""6"" /></StartLayoutCollection></DefaultLayoutOverride></LayoutModificationTemplate>'");
                sb.AppendLine(@"    Set-Content -Path ""$layoutDir\LayoutModification.xml"" -Value $xmlLayout -Force");

                sb.AppendLine(@"    Remove-Item ""$mountDir\Users\Default\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start.bin"" -Force -ErrorAction SilentlyContinue");

                sb.AppendLine(@"    $scriptsDir = ""$mountDir\ProgramData\EvolveOS\Scripts""");
                sb.AppendLine(@"    if (-not (Test-Path $scriptsDir)) { New-Item -Path $scriptsDir -ItemType Directory -Force | Out-Null }");
                sb.AppendLine(@"    $psScriptPath = Join-Path $scriptsDir 'RemoveGhostApps.ps1'");
                sb.AppendLine(@"    Set-Content -Path $psScriptPath -Value '$ErrorActionPreference = ""SilentlyContinue""' -Force");
                foreach (var app in options.AppsToRemove)
                {
                    sb.AppendLine($"    Add-Content -Path $psScriptPath -Value \"Get-AppxPackage -AllUsers '*{app}*' | Remove-AppxPackage -AllUsers\"");
                }
            }

            if (options.EnableNet35)
            {
                sb.AppendLine($"    Write-Output '{msgEnableNet35}'");
                sb.AppendLine(@"    $sxsPath = Join-Path $workDir 'sources\sxs'");
                sb.AppendLine(@"    if (Test-Path ""$sxsPath\*.cab"") {");
                sb.AppendLine(@"        Enable-WindowsOptionalFeature -Path $mountDir -FeatureName NetFx3 -All -LimitAccess -Source $sxsPath | Out-Null");
                sb.AppendLine(@"    }");
            }

            if (options.RemoveWindowsRecovery)
            {
                sb.AppendLine($"    Write-Output '{msgWipeWinRe}'");
                sb.AppendLine(@"    $winre = ""$mountDir\Windows\System32\Recovery\winre.wim""");
                sb.AppendLine(@"    $reagent = ""$mountDir\Windows\System32\Recovery\ReAgent.xml""");

                sb.AppendLine(@"    foreach ($file in @($winre, $reagent)) {");
                sb.AppendLine(@"        if (Test-Path -LiteralPath $file) {");
                sb.AppendLine($"            Write-Output \">> {msgStripProtect} $file...\"");
                sb.AppendLine(@"            takeown.exe /f $file /a | Out-Null");
                sb.AppendLine(@"            icacls.exe $file /grant ""Administrators:F"" /q | Out-Null");
                sb.AppendLine(@"            attrib.exe -s -h -r $file | Out-Null");

                sb.AppendLine(@"            $retries = 0");
                sb.AppendLine(@"            while ((Test-Path -LiteralPath $file) -and ($retries -lt 10)) {");
                sb.AppendLine($"                Write-Output \">> {msgAttDelete} ($retries/10)\"");
                sb.AppendLine(@"                Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue");
                sb.AppendLine(@"                if (Test-Path -LiteralPath $file) { Start-Sleep -Seconds 2 }");
                sb.AppendLine(@"                $retries++");
                sb.AppendLine(@"            }");
                sb.AppendLine(@"        }");
                sb.AppendLine(@"    }");

                sb.AppendLine(@"    reg.exe load HKLM\OffSoft ""$mountDir\Windows\System32\config\SOFTWARE"" 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe add ""HKLM\OffSoft\Microsoft\Windows\CurrentVersion\ReserveManager"" /v ""ShippedWithWinRE"" /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
                sb.AppendLine(@"    reg.exe unload HKLM\OffSoft 2>&1 | Out-Null");

                sb.AppendLine($"    Write-Output '{msgGenDummy}'");
                sb.AppendLine(@"    $emptyDir = Join-Path $workDir 'EmptyWinRE'");
                sb.AppendLine(@"    if (-not (Test-Path $emptyDir)) { New-Item -ItemType Directory -Path $emptyDir -Force | Out-Null }");
                sb.AppendLine(@"    dism.exe /Capture-Image /CaptureDir:$emptyDir /ImageFile:$winre /Name:""EmptyWinRE"" /Compress:max | Out-Null");
            }

            sb.AppendLine($"    Write-Output '{msgDeepClean}'");
            sb.AppendLine(@"    dism.exe /Image:$mountDir /Cleanup-Image /StartComponentCleanup /ResetBase | Out-Null");

            sb.AppendLine($"    Write-Output '{msgPurgeCache}'");
            sb.AppendLine(@"    $cachePaths = @(");
            sb.AppendLine(@"        ""$mountDir\Windows\Logs\CBS\*"",");
            sb.AppendLine(@"        ""$mountDir\Windows\Logs\DISM\*"",");
            sb.AppendLine(@"        ""$mountDir\Windows\Temp\*"",");
            sb.AppendLine(@"        ""$mountDir\Users\Default\AppData\Local\Temp\*"",");
            sb.AppendLine(@"        ""$mountDir\Windows\SoftwareDistribution\Download\*"",");
            sb.AppendLine(@"        ""$mountDir\Windows\SoftwareDistribution\DataStore\*"",");
            sb.AppendLine(@"        ""$mountDir\Windows\Prefetch\*"",");
            sb.AppendLine(@"        ""$mountDir\Windows\WinSxS\Backup\*"",");
            sb.AppendLine(@"        ""$mountDir\Windows\System32\SleepStudy\*""");
            sb.AppendLine(@"    )");
            sb.AppendLine(@"    foreach ($path in $cachePaths) { Remove-Item -Path $path -Force -Recurse -ErrorAction SilentlyContinue }");

            sb.AppendLine("} finally {");
            sb.AppendLine("    [System.GC]::Collect(); [System.GC]::WaitForPendingFinalizers(); Start-Sleep -Seconds 2");
            sb.AppendLine(@"    reg.exe unload HKLM\OffSoft 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe unload HKLM\OffSys 2>&1 | Out-Null");
            sb.AppendLine(@"    reg.exe unload HKLM\OffDef 2>&1 | Out-Null");

            sb.AppendLine($"    Write-Output '{msgSaveWim}'");
            sb.AppendLine("    dism.exe /Unmount-Image /MountDir:$mountDir /Commit | Out-Null");

            sb.AppendLine(@"    Remove-MpPreference -ExclusionPath $workDir -ErrorAction SilentlyContinue");

            sb.AppendLine("    Remove-Item -Path $mountDir -Force -Recurse -ErrorAction SilentlyContinue");

            if (options.ImageFormat != null && options.ImageFormat.Equals("ESD", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"    Write-Output '{msgCompressEsd}'");
                sb.AppendLine("    dism.exe /Export-Image /SourceImageFile:$wimPath /SourceIndex:1 /DestinationImageFile:$esdPath /Compress:recovery /CheckIntegrity | Out-Null");
                sb.AppendLine($"    if (Test-Path $esdPath) {{ Remove-Item $wimPath -Force }} else {{ throw '{errEsd}' }}");
            }
            else
            {
                sb.AppendLine($"    Write-Output '{msgReclaimSpace}'");
                sb.AppendLine("    $optimizedWim = Join-Path $workDir 'sources\\install_optimized.wim'");
                sb.AppendLine("    dism.exe /Export-Image /SourceImageFile:$wimPath /SourceIndex:1 /DestinationImageFile:$optimizedWim /Compress:max /CheckIntegrity | Out-Null");
                sb.AppendLine($"    if (Test-Path $optimizedWim) {{ Remove-Item $wimPath -Force; Rename-Item $optimizedWim 'install.wim' }} else {{ throw '{errWim}' }}");
            }

            sb.AppendLine("    Stop-Transcript");
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
                        progress.Report(e.Data);
                };
                process.BeginOutputReadLine();

                using var ctr = cancellationToken.Register(() => { try { process.Kill(true); } catch { } });

                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                string error = await errorTask;

                if (process.ExitCode != 0)
                {
                    string errService = ResourceString.GetString("isobuilder_bg_err_service") ?? "Failed to service offline WIM. Error:";
                    throw new Exception($"{errService} {error}");
                }
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
                xmlSb.AppendLine($"        <SynchronousCommand wcm:action=\"add\"><Order>{logonOrder++}</Order><CommandLine>powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"C:\\ProgramData\\EvolveOS\\Scripts\\RemoveGhostApps.ps1\"</CommandLine></SynchronousCommand>");
            }

            xmlSb.AppendLine("      </FirstLogonCommands>");
            xmlSb.AppendLine("    </component>");
            xmlSb.AppendLine("  </settings>");
            xmlSb.AppendLine("</unattend>");

            File.WriteAllText(xmlPath, xmlSb.ToString(), Encoding.UTF8);
        }

        private async Task RepackIsoAsync(string oscdimgPath, string workingDir, string outputIsoPath, IProgress<string> progress, CancellationToken cancellationToken)
        {
            string etfsBoot = Path.Combine(workingDir, "boot", "etfsboot.com");
            string efiSys = Path.Combine(workingDir, "efi", "microsoft", "boot", "efisys.bin");

            if (!File.Exists(etfsBoot) || !File.Exists(efiSys))
            {
                string errMsg = ResourceString.GetString("isobuilder_bg_err_missing_boot") ?? "Extracted ISO is missing boot files. Ensure you selected a valid Windows ISO.";
                throw new Exception(errMsg);
            }

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
                        progress.Report(e.Data);
                };
                process.BeginOutputReadLine();

                using var ctr = cancellationToken.Register(() => { try { process.Kill(true); } catch { } });

                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                string error = await errorTask;

                if (process.ExitCode != 0)
                {
                    string errPack = ResourceString.GetString("isobuilder_bg_err_pack_failed") ?? "oscdimg failed to pack the ISO. Error:";
                    throw new Exception($"{errPack} {error}");
                }
            }
        }
    }
}