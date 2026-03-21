// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Text;

namespace EvolveOS_Optimizer.Utilities.WinBuilder
{
    public static class EvolveOSScripts
    {
        public static string GenerateMasterScript(IsoBuildOptions options)
        {
            var sb = new StringBuilder();

            sb.AppendLine("""
<#
.SYNOPSIS
    EvolveOS Windows 10/11 Customization and Optimization Script
.DESCRIPTION
    Applies registry settings, UWP app removals, optimizations and customizations based on Windows version detection
.NOTES
    Requires Administrator privileges
    Compatible with Windows 10 and Windows 11
.PARAMETER UserCustomizations
    When specified, applies ONLY HKCU (user-specific) registry settings.
    When not specified, applies all settings EXCEPT HKCU entries.
#>

param(
    [switch]$UserCustomizations
)

$LogPath = 'C:\ProgramData\EvolveOS\Unattend\Logs\EvolveOS.txt'
$null = New-Item -Path (Split-Path $LogPath) -ItemType Directory -Force

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet("INFO", "SUCCESS", "WARNING", "ERROR")]
        [string]$Level = "INFO"
    )
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogEntry = "[$Timestamp] [$Level] $Message"
    Add-Content -Path $LogPath -Value $LogEntry -Encoding UTF8
}

Write-Log "=================================================================================" "INFO"
Write-Log "EvolveOS Windows Optimization & Customization Script Started" "INFO"
if ($UserCustomizations) {
    Write-Log "MODE: User Customizations Only (HKCU registry entries)" "INFO"
} else {
    Write-Log "MODE: System Customizations (All settings except HKCU entries)" "INFO"
}
Write-Log "=================================================================================" "INFO"

function Get-TargetUser {
    try {
        $user = Get-WmiObject Win32_ComputerSystem | Select-Object -ExpandProperty UserName
        if ($user -and $user -ne "NT AUTHORITY\SYSTEM") {
            $username = $user.Split('\')[1]
            if ($username -ne "defaultuser0") { return $username }
        }
    } catch { }
    try {
        $explorer = Get-Process explorer -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($explorer) {
            $owner = $explorer.GetOwner()
            if ($owner.User -ne "defaultuser0") { return $owner.User }
        }
    } catch { }
    return $null
}

function Get-UserSID {
    param($Username)
    try {
        $profListPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList'
        foreach ($key in Get-ChildItem $profListPath -ErrorAction SilentlyContinue) {
            $profPath = (Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue).ProfileImagePath
            if ($profPath -and $profPath.EndsWith("\$Username")) { return $key.PSChildName }
        }
        return $null
    } catch { return $null }
}

function Start-ProcessAsUser {
    param([string]$CommandLine)
    if (-not ([System.Management.Automation.PSTypeName]'EvolveOS.TL'.Type)) {
        Add-Type -MemberDefinition @'
[DllImport("advapi32.dll",SetLastError=true)]public static extern bool OpenProcessToken(IntPtr h,uint a,out IntPtr t);
[DllImport("advapi32.dll",SetLastError=true)]public static extern bool GetTokenInformation(IntPtr t,int c,IntPtr i,int l,out int r);
[DllImport("advapi32.dll",SetLastError=true)]public static extern bool DuplicateTokenEx(IntPtr t,uint a,IntPtr s,int il,int tt,out IntPtr n);
[DllImport("advapi32.dll",SetLastError=true,CharSet=CharSet.Unicode)]public static extern bool CreateProcessAsUserW(IntPtr t,string app,string cmd,IntPtr pa,IntPtr ta,bool ih,int cf,IntPtr env,string dir,ref SI si,out PI pi);
[DllImport("kernel32.dll",SetLastError=true)]public static extern bool CloseHandle(IntPtr h);
[DllImport("kernel32.dll")]public static extern uint WTSGetActiveConsoleSessionId();
[DllImport("kernel32.dll",SetLastError=true)]public static extern bool ProcessIdToSessionId(uint p,out uint s);
[DllImport("kernel32.dll",SetLastError=true)]public static extern uint WaitForSingleObject(IntPtr h,uint ms);
[DllImport("kernel32.dll",SetLastError=true)]public static extern bool GetExitCodeProcess(IntPtr h,out uint c);
[DllImport("userenv.dll",SetLastError=true)]public static extern bool CreateEnvironmentBlock(out IntPtr env,IntPtr token,bool inherit);
[DllImport("userenv.dll",SetLastError=true)]public static extern bool DestroyEnvironmentBlock(IntPtr env);
[StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct SI{public int cb;public string r1,d,t;public int x,y,w,h,cc,cr,fa,fl;public short sw,r2;public IntPtr r3,i,o,e;}
[StructLayout(LayoutKind.Sequential)]public struct PI{public IntPtr hp,ht;public int pid,tid;}
'@ -Name TL -Namespace EvolveOS -ErrorAction Stop
    }
    $T = [EvolveOS.TL]; $tok = $dup = $envBlock = [IntPtr]::Zero; $pi = New-Object EvolveOS.TL+PI; $launched = $false
    try {
        $cs = $T::WTSGetActiveConsoleSessionId(); if ($cs -eq 0xFFFFFFFF) { return $false }
        $ep = $null; foreach ($p in (Get-Process explorer -ErrorAction SilentlyContinue)) { $s = [uint32]0; if ($T::ProcessIdToSessionId([uint32]$p.Id, [ref]$s) -and $s -eq $cs) { $ep = $p; break } }
        if (-not $ep) { return $false }
        if (-not $T::OpenProcessToken($ep.Handle, 0x000A, [ref]$tok)) { return $false }
        $dupSrc = $tok; $linked = [IntPtr]::Zero; $eb = [Runtime.InteropServices.Marshal]::AllocHGlobal(4); $rl = 0
        if ($T::GetTokenInformation($tok, 18, $eb, 4, [ref]$rl) -and [Runtime.InteropServices.Marshal]::ReadInt32($eb) -eq 3) {
            $lb = [Runtime.InteropServices.Marshal]::AllocHGlobal([IntPtr]::Size)
            if ($T::GetTokenInformation($tok, 19, $lb, [IntPtr]::Size, [ref]$rl)) { $linked = [Runtime.InteropServices.Marshal]::ReadIntPtr($lb); $dupSrc = $linked }
            [Runtime.InteropServices.Marshal]::FreeHGlobal($lb)
        }
        [Runtime.InteropServices.Marshal]::FreeHGlobal($eb)
        if (-not $T::DuplicateTokenEx($dupSrc, 0xF01FF, [IntPtr]::Zero, 2, 1, [ref]$dup)) { return $false }
        if ($linked -ne [IntPtr]::Zero) { $null = $T::CloseHandle($linked) }
        $null = $T::CreateEnvironmentBlock([ref]$envBlock, $dup, $false); $si = New-Object EvolveOS.TL+SI; $si.cb = [Runtime.InteropServices.Marshal]::SizeOf($si); $si.d = "winsta0\default"; $psExe = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
        if (-not $T::CreateProcessAsUserW($dup, $psExe, $CommandLine, [IntPtr]::Zero, [IntPtr]::Zero, $false, 0x08000400, $envBlock, $env:SystemRoot, [ref]$si, [ref]$pi)) { return $false }
        $launched = $true; $wait = $T::WaitForSingleObject($pi.hp, 600000); if ($wait -ne 0) { return $false }
        $ec = [uint32]0; $null = $T::GetExitCodeProcess($pi.hp, [ref]$ec); return ($ec -eq 0)
    } catch { return $false } finally {
        if ($launched) { if ($pi.ht -ne [IntPtr]::Zero) { $null = $T::CloseHandle($pi.ht) }; if ($pi.hp -ne [IntPtr]::Zero) { $null = $T::CloseHandle($pi.hp) } }
        if ($envBlock -ne [IntPtr]::Zero) { $null = $T::DestroyEnvironmentBlock($envBlock) }; if ($dup -ne [IntPtr]::Zero) { $null = $T::CloseHandle($dup) }; if ($tok -ne [IntPtr]::Zero) { $null = $T::CloseHandle($tok) }
    }
}
""");

            sb.AppendLine("if (-not $UserCustomizations) {");
            sb.AppendLine("    $scriptsDir = \"C:\\ProgramData\\EvolveOS\\Scripts\"");
            sb.AppendLine("    if (!(Test-Path $scriptsDir)) { New-Item -ItemType Directory -Path $scriptsDir -Force | Out-Null }");
            sb.AppendLine("    $bloatRemovalContent = @'");
            sb.AppendLine("<# .SYNOPSIS Removes Windows bloatware apps #>");
            sb.AppendLine("If (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]\"Administrator\")) { Try { Start-Process PowerShell.exe -ArgumentList (\"-NoProfile -ExecutionPolicy Bypass -File `\"{0}`\"\" -f $PSCommandPath) -Verb RunAs; Exit } Catch { Exit } }");
            sb.AppendLine("$logFolder = \"C:\\ProgramData\\EvolveOS\\Logs\"; $logFile = \"$logFolder\\BloatRemovalLog.txt\"");
            sb.AppendLine("if (!(Test-Path $logFolder)) { New-Item -ItemType Directory -Path $logFolder -Force | Out-Null }");
            sb.AppendLine("function Write-Log { param ([string]$Message) \"$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - $Message\" | Out-File -FilePath $logFile -Append; Write-Host $Message }");
            sb.AppendLine("function Invoke-RunspacePool { param ([array]$Items, [scriptblock]$ScriptBlock, [int]$MaxThreads = 10, [string]$Label, [string]$SuccessFormat, [string]$FailFormat) if ($Items.Count -eq 0) { return }; try { $pool = [RunspaceFactory]::CreateRunspacePool(1, [Math]::Min($Items.Count, $MaxThreads)); $pool.Open(); $jobs = [System.Collections.Generic.List[object]]::new(); foreach ($item in $Items) { $ps = [PowerShell]::Create().AddScript($ScriptBlock).AddArgument($item); $ps.RunspacePool = $pool; $jobs.Add(@{ Pipe = $ps; Handle = $ps.BeginInvoke() }) }; foreach ($job in $jobs) { $result = $job.Pipe.EndInvoke($job.Handle); foreach ($r in $result) { if ($r.Success) { Write-Log ($SuccessFormat -f $r.Name) } else { Write-Log ($FailFormat -f $r.Name, $r.Error) } }; $job.Pipe.Dispose() }; $pool.Close(); $pool.Dispose() } catch { foreach ($item in $Items) { $r = & $ScriptBlock $item; if ($r.Success) { Write-Log ($SuccessFormat -f $r.Name) } else { Write-Log ($FailFormat -f $r.Name, $r.Error) } } } }");
            sb.AppendLine("$packages = @(");
            foreach (var app in options.AppsToRemove) sb.AppendLine($"    '{app}'");
            sb.AppendLine(")");

            sb.AppendLine("$capabilities = @('Microsoft.Windows.PowerShell.ISE', 'App.Support.QuickAssist', 'App.StepsRecorder', 'Microsoft.Windows.WordPad', 'Microsoft.Windows.MSPaint')");
            sb.AppendLine("$optionalFeatures = @('Recall')");
            sb.AppendLine("$specialApps = @('OneNote')");
            sb.AppendLine("Write-Log \"Discovering all packages...\"");
            sb.AppendLine("$allInstalled = Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue; $allProvisioned = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue");
            sb.AppendLine("$packagesToRemove = @(); $provisionedToRemove = @()");
            sb.AppendLine("foreach ($package in $packages) { $installed = @($allInstalled | Where-Object Name -match $package); $provisioned = @($allProvisioned | Where-Object DisplayName -match $package); if ($installed) { foreach ($pkg in $installed) { $packagesToRemove += $pkg.PackageFullName } }; if ($provisioned) { foreach ($pkg in $provisioned) { $provisionedToRemove += $pkg.PackageName } } }");
            sb.AppendLine("Invoke-RunspacePool -Items $provisionedToRemove -MaxThreads 10 -Label \"provisioned packages\" -ScriptBlock { param($p) try { Remove-AppxProvisionedPackage -Online -PackageName $p -ErrorAction Stop | Out-Null; @{ Name = $p; Success = $true; Error = $null } } catch { @{ Name = $p; Success = $false; Error = $_.Exception.Message } } } -SuccessFormat \"Deprovisioned: {0}\" -FailFormat \"Failed to deprovision {0}: {1}\"");
            sb.AppendLine("Invoke-RunspacePool -Items $packagesToRemove -MaxThreads 10 -Label \"installed packages\" -ScriptBlock { param($p) try { Remove-AppxPackage -Package $p -AllUsers -ErrorAction Stop; @{ Name = $p; Success = $true; Error = $null } } catch { @{ Name = $p; Success = $false; Error = $_.Exception.Message } } } -SuccessFormat \"Removed installed package: {0}\" -FailFormat \"Failed to remove installed package {0}: {1}\"");
            sb.AppendLine("Write-Log \"Bloat removal process completed\"");
            sb.AppendLine("'@");
            sb.AppendLine("    $bloatRemovalPath = Join-Path $scriptsDir \"BloatRemoval.ps1\"");
            sb.AppendLine("    try { $bloatRemovalContent | Out-File -FilePath $bloatRemovalPath -Encoding UTF8 -Force } catch { }");

            if (options.RemoveMicrosoftEdge)
            {
                sb.AppendLine("    $edgeRemovalContent = @'");
                sb.AppendLine(EdgeRemovalScript);
                sb.AppendLine("'@");
                sb.AppendLine("    $edgeRemovalPath = Join-Path $scriptsDir \"EdgeRemoval.ps1\"");
                sb.AppendLine("    try { $edgeRemovalContent | Out-File -FilePath $edgeRemovalPath -Encoding UTF8 -Force } catch { }");
            }

            if (options.RemoveOneDrive)
            {
                sb.AppendLine("    $oneDriveRemovalContent = @'");
                sb.AppendLine(OneDriveRemovalScript);
                sb.AppendLine("'@");
                sb.AppendLine("    $oneDriveRemovalPath = Join-Path $scriptsDir \"OneDriveRemoval.ps1\"");
                sb.AppendLine("    try { $oneDriveRemovalContent | Out-File -FilePath $oneDriveRemovalPath -Encoding UTF8 -Force } catch { }");
            }

            sb.AppendLine("    $scriptsToExecute = @()");
            sb.AppendLine("    $scriptsToExecute += @{Path = \"$scriptsDir\\BloatRemoval.ps1\"; Name = \"BloatRemoval\"; TriggerType = \"Logon\"}");
            if (options.RemoveMicrosoftEdge) sb.AppendLine("    $scriptsToExecute += @{Path = \"$scriptsDir\\EdgeRemoval.ps1\"; Name = \"EdgeRemoval\"; TriggerType = \"Startup\"}");
            if (options.RemoveOneDrive) sb.AppendLine("    $scriptsToExecute += @{Path = \"$scriptsDir\\OneDriveRemoval.ps1\"; Name = \"OneDriveRemoval\"; TriggerType = \"Logon\"}");

            sb.AppendLine(@"
    foreach ($script in $scriptsToExecute) {
        if (Test-Path $script.Path) {
            try { Start-Process powershell.exe -ArgumentList ""-ExecutionPolicy Bypass -NoProfile -File `""$($script.Path)`"""" -Wait -NoNewWindow } catch { }
            try {
                $action = New-ScheduledTaskAction -Execute ""powershell.exe"" -Argument ""-ExecutionPolicy Bypass -NoProfile -File `""$($script.Path)`""""
                if ($script.TriggerType -eq ""Startup"") { $trigger = New-ScheduledTaskTrigger -AtStartup } else { $trigger = New-ScheduledTaskTrigger -AtLogon }
                $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0
                $principal = New-ScheduledTaskPrincipal -UserId ""SYSTEM"" -LogonType ServiceAccount -RunLevel Highest
                Register-ScheduledTask -TaskName $script.Name -TaskPath ""\EvolveOS"" -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
            } catch { }
        }
    }
    try {
        $action = New-ScheduledTaskAction -Execute ""powershell.exe"" -Argument ""-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File C:\ProgramData\EvolveOS\Unattend\Scripts\EvolveOS.ps1 -UserCustomizations""
        $trigger = New-ScheduledTaskTrigger -AtLogOn
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0
        $principal = New-ScheduledTaskPrincipal -UserId ""SYSTEM"" -LogonType ServiceAccount -RunLevel Highest
        Register-ScheduledTask -TaskName ""EvolveOSUserCustomizations"" -TaskPath ""\EvolveOS"" -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
    } catch { }
");

            foreach (var svc in options.ServiceTweaks)
            {
                string val = svc.StartupType.ToLower() switch { "disabled" => "4", "manual" => "3", "automatic" => "2", "automaticdelayedstart" => "2", _ => "3" };
                sb.AppendLine($"    reg.exe add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\{svc.ServiceName}\" /v Start /t REG_DWORD /d {val} /f 2>&1 | Out-Null");
            }

            foreach (var tweak in options.RegistryTweaks.Where(t => !t.RegCommand.Contains("HKCU")))
            {
                string cmd = tweak.RegCommand.StartsWith("reg", StringComparison.OrdinalIgnoreCase) ? tweak.RegCommand.Replace("reg ", "reg.exe ") : tweak.RegCommand;
                sb.AppendLine($"    {cmd} 2>&1 | Out-Null");
            }
            if (options.DisableHibernate) sb.AppendLine("    powercfg.exe -h off 2>&1 | Out-Null");

            sb.AppendLine("}");

            sb.AppendLine("if ($UserCustomizations) {");
            sb.AppendLine(@"
    $runningAsSystem = ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value -eq 'S-1-5-18')
    if ($runningAsSystem) {
        if (-not (Test-Path ""HKU:\"")) { New-PSDrive -PSProvider Registry -Name HKU -Root HKEY_USERS -ErrorAction SilentlyContinue | Out-Null }
        $targetUser = $null
        for ($attempt = 1; $attempt -le 12; $attempt++) { $targetUser = Get-TargetUser; if ($targetUser) { break }; Start-Sleep -Seconds 10 }
        if (-not $targetUser) { exit 1 }
        $targetUserSID = Get-UserSID -Username $targetUser; if (-not $targetUserSID) { exit 1 }
        $markerPath = ""HKU:\$targetUserSID\Software\EvolveOS""; $markerName = ""UserCustomizationsApplied""; $alreadyApplied = $false
        try { if (Test-Path $markerPath) { $value = Get-ItemProperty -Path $markerPath -Name $markerName -ErrorAction SilentlyContinue; if ($value.$markerName -eq 1) { $alreadyApplied = $true } } } catch { }
        if (-not $alreadyApplied) {
            icacls $LogPath /grant ""${targetUser}:(M)"" 2>&1 | Out-Null
            $scriptPath = $MyInvocation.MyCommand.Path
            $cmdLine = ""powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File `""$scriptPath`"" -UserCustomizations""
            $success = Start-ProcessAsUser -CommandLine $cmdLine
            if ($success) { shutdown.exe /r /t 20 } else { exit 1 }
        }
    } else {
        $markerPath = ""HKCU:\Software\EvolveOS""; $markerName = ""UserCustomizationsApplied""; $alreadyApplied = $false
        try { if (Test-Path $markerPath) { $value = Get-ItemProperty -Path $markerPath -Name $markerName -ErrorAction SilentlyContinue; if ($value.$markerName -eq 1) { $alreadyApplied = $true } } } catch { }
        if (-not $alreadyApplied) {
            Write-Log ""Applying user customizations for the first time..."" ""INFO""
");

            foreach (var tweak in options.RegistryTweaks.Where(t => t.RegCommand.Contains("HKCU")))
            {
                string cmd = tweak.RegCommand.StartsWith("reg", StringComparison.OrdinalIgnoreCase) ? tweak.RegCommand.Replace("reg ", "reg.exe ") : tweak.RegCommand;
                sb.AppendLine($"            {cmd} 2>&1 | Out-Null");
            }
            if (options.AlignTaskbarLeft) sb.AppendLine("            reg.exe add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarAl /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
            if (options.ForceDarkMode)
            {
                sb.AppendLine("            reg.exe add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\" /v AppsUseLightTheme /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
                sb.AppendLine("            reg.exe add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\" /v SystemUsesLightTheme /t REG_DWORD /d 0 /f 2>&1 | Out-Null");
            }

            sb.AppendLine(@"
            try {
                if (-not (Test-Path $markerPath)) { New-Item -Path $markerPath -Force | Out-Null }
                Set-ItemProperty -Path $markerPath -Name $markerName -Value 1 -Type DWord -Force
                Write-Log ""User customizations completed and marked as applied"" ""SUCCESS""
            } catch { }
        }
    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        public static readonly string EdgeRemovalScript = """
<#
  .SYNOPSIS
      Removes Microsoft Edge (Legacy and Chromium versions) from Windows 10/11 systems.
#>
$logFolder = "C:\ProgramData\EvolveOS\Logs"
$logFile = "$logFolder\EdgeRemovalLog.txt"
if (!(Test-Path $logFolder)) { New-Item -ItemType Directory -Path $logFolder -Force | Out-Null }
function Write-Log { param ([string]$Message) "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - $Message" | Out-File -FilePath $logFile -Append; Write-Host $Message }

function Get-LegacyEdgePackages {
    $legacyRegPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages"
    return Get-ChildItem -Path $legacyRegPath -Name -ErrorAction SilentlyContinue | Where-Object { $_ -match "Microsoft-Windows-Internet-Browser-Package" -and $_ -match "~~" }
}

function Test-LegacyEdgeInstalled {
    $packages = Get-LegacyEdgePackages
    if ($packages) {
        foreach ($package in $packages) {
            $packageInfo = & dism /online /Get-PackageInfo /PackageName:$package 2>$null
            if ($packageInfo -match "State.*Installed") { return $true }
        }
    }
    return $false
}

function Test-ChromiumEdgeInstalled {
    $edgeFolders = @("Edge", "EdgeCore", "EdgeUpdate")
    $programFiles = @($env:ProgramFiles, ${env:ProgramFiles(x86)})
    foreach ($pf in $programFiles) { foreach ($folder in $edgeFolders) { if (Test-Path "$pf\Microsoft\$folder") { return $true } } }
    try {
        $edgeApp = Get-WmiObject -Class Win32_InstalledStoreProgram -Filter "Name like '%Microsoft.MicrosoftEdge.Stable%'" -ErrorAction SilentlyContinue
        return $edgeApp -ne $null
    } catch { return $false }
}

function Stop-EdgeProcesses {
    Write-Log "Stopping Edge-related processes and services"
    $stop = "MicrosoftEdgeUpdate", "OneDrive", "WidgetService", "Widgets", "msedge", "Resume", "CrossDeviceResume"
    $stop | ForEach-Object {
        $processCount = (Get-Process -Name $_ -ErrorAction SilentlyContinue).Count
        if ($processCount -gt 0) {
            Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue
            Write-Log "Stopped $processCount instance(s) of $_"
        }
    }
}

function Remove-LegacyEdge {
    Write-Log "Starting Legacy Edge/UWP Edge removal process"
    $packages = Get-LegacyEdgePackages
    $edgeLegacyPackageVersion = $packages | Select-Object -First 1
    $packagePath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages\$edgeLegacyPackageVersion"
    Set-ItemProperty -Path $packagePath -Name "Visibility" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    $ownersPath = "$packagePath\Owners"
    if (Test-Path $ownersPath) { Remove-Item -Path $ownersPath -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Log "Removing Legacy Edge package via DISM"
    $dismProcess = Start-Process -FilePath "dism.exe" -ArgumentList "/online", "/Remove-Package", "/PackageName:$edgeLegacyPackageVersion" -NoNewWindow -PassThru
    if ($dismProcess -and $dismProcess.WaitForExit(30000)) { Write-Log "DISM completed successfully" }
    elseif ($dismProcess) {
        $dismProcess.Kill(); Start-Sleep 2
        $retryProcess = Start-Process -FilePath "dism.exe" -ArgumentList "/online", "/Remove-Package", "/PackageName:$edgeLegacyPackageVersion" -NoNewWindow -PassThru
        if ($retryProcess -and $retryProcess.WaitForExit(30000)) { Write-Log "DISM retry completed successfully" }
        elseif ($retryProcess) { $retryProcess.Kill() }
    }
    Write-Log "Removing Legacy UWP Edge package"
    Get-AppxPackage -AllUsers Microsoft.MicrosoftEdge | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue | Out-Null
}

function Remove-EdgeShortcuts {
    Write-Log "Starting Edge shortcuts cleanup"
    $userProfiles = Get-ChildItem -Path "C:\Users" -Directory | Where-Object { (Test-Path -Path "$($_.FullName)\NTUSER.DAT") }
    $shortcutPaths = @()
    foreach ($profile in $userProfiles) {
        $shortcutPaths += @(
            "$($profile.FullName)\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\Microsoft Edge.lnk",
            "$($profile.FullName)\Desktop\Microsoft Edge.lnk",
            "$($profile.FullName)\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Microsoft Edge.lnk",
            "$($profile.FullName)\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Tombstones\Microsoft Edge.lnk",
            "$($profile.FullName)\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk"
        )
    }
    $shortcutPaths += "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk"
    $removedCount = 0
    foreach ($path in $shortcutPaths) {
        if (Test-Path -Path $path -PathType Leaf) { Remove-Item -Path $path -Force -ErrorAction SilentlyContinue; $removedCount++ }
    }
    Write-Log "Removed $removedCount Edge shortcut(s)"
}

function Install-EdgeProtocolRedirect {
    Write-Log "Installing Edge protocol redirect using OpenWebSearch"
    $scriptsDir = "C:\ProgramData\EvolveOS\OpenWebSearch"
    New-Item -ItemType Directory -Path $scriptsDir -Force -ErrorAction SilentlyContinue | Out-Null
    $stubTargetPath = "$scriptsDir\ie_to_edge_stub.exe"
    if (!(Test-Path $stubTargetPath)) { Write-Log "Warning: ie_to_edge_stub.exe not found at $stubTargetPath"; return }

    $openWebSearchContent = @"
@title OpenWebSearch 2023 & echo off
for /f %%E in ('"prompt `$E`$S& for %%e in (1) do rem"') do echo;%%E[2t 2>nul
call :reg_var "HKCU\SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoiceLatest\ProgId" ProgID ProgID
if not defined ProgID call :reg_var "HKCU\SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice" ProgID ProgID
if /i "%ProgID%" neq "MSEdgeHTM" if defined ProgID goto :browser_found
set "Choice="
for %%R in (HKCU HKLM) do (
    for /f "delims=" %%K in ('reg query "%%R\SOFTWARE\Clients\StartMenuInternet" 2^>nul') do (
        for /f "skip=1 tokens=2*" %%A in ('reg query "%%K\shell\open\command" /ve 2^>nul') do (
            echo "%%B" | findstr /i "msedge ie_to_edge_stub iexplore" >nul || (set "Choice=%%~B" & goto :skip_browser)
        )
    )
)
if not defined Choice exit /b
:browser_found
call :reg_var "HKCR\%ProgID%\shell\open\command" "" Browser
set Choice=& for %%. in (%Browser%) do if not defined Choice set "Choice=%%~."
:skip_browser
set "URI=" & set "URL=" & set "NOOP="
set "CLI=%CMDCMDLINE:"=``%"
if defined CLI set "CLI=%CLI:*ie_to_edge_stub.exe`` =%"
if defined CLI set "CLI=%CLI:*ie_to_edge_stub.exe =%"
if defined CLI set "CLI=%CLI:*msedge.exe`` =%"
if defined CLI set "CLI=%CLI:*msedge.exe =%"
set "FIX=%CLI:~-1%"
if defined CLI if "%FIX%"==" " set "CLI=%CLI:~0,-1%"
if defined CLI set "RED=%CLI:microsoft-edge=%"
if defined CLI set "URL=%CLI:http=%"
if "%CLI%" equ "%RED%" (set NOOP=1) else if "%CLI%" equ "%URL%" (set NOOP=1)
if defined NOOP exit /b
set "URL=%CLI:*microsoft-edge=%"
set "URL=http%URL:*http=%"
set "FIX=%URL:~-2%"
if defined URL if "%FIX%"=="//" set "URL=%URL:~0,-1%"
call :dec_url
start "" "%Choice%" "%URL%"
exit

:reg_var
set {var}=& set {reg}=reg query "%~1" /v %2 /z /se "," /f /e& if %2=="" set {reg}=reg query "%~1" /ve /z /se "," /f /e
for /f "skip=2 tokens=* delims=" %%V in ('%{reg}% %4 %5 %6 %7 %8 %9 2^>nul') do if not defined {var} set "{var}=%%V"
if not defined {var} (set {reg}=& set "%~3="& exit /b) else if %2=="" set "{var}=%{var}:*)    =%"
if not defined {var} (set {reg}=& set "%~3="& exit /b) else set {reg}=& set "%~3=%{var}:*)    =%"& set {var}=& exit /b

:dec_url
set ".=%URL:!=}%" & setlocal enabledelayedexpansion
set ".=!.:%%={!" &set ".=!.:{3A=:!" &set ".=!.:{2F=/!" &set ".=!.:{3F=?!" &set ".=!.:{23=#!" &set ".=!.:{5B=[!" &set ".=!.:{5D=]!"
set ".=!.:{40=@!"&set ".=!.:{21=}!" &set ".=!.:{24=`$!" &set ".=!.:{26=&!" &set ".=!.:{27='!" &set ".=!.:{28=(!" &set ".=!.:{29=)!"
set ".=!.:{2A=*!"&set ".=!.:{2B=+!" &set ".=!.:{2C=,!" &set ".=!.:{3B=;!" &set ".=!.:{3D==!" &set ".=!.:{25=%%!"&set ".=!.:{20= !"
set ".=!.:{=%%!" & endlocal& set "URL=%.:}=!%" & exit /b
"@

    $openWebSearchPath = "$scriptsDir\OpenWebSearch.cmd"
    $openWebSearchContent | Out-File -FilePath $openWebSearchPath -Encoding ASCII -Force
    Write-Log "Created OpenWebSearch.cmd at $openWebSearchPath"

    $buildNumber = [Environment]::OSVersion.Version.Build
    $conhostFlags = if ($buildNumber -gt 25179) { "--width 1 --height 1" } else { "--headless" }
    $conhostDebugger = "$env:SystemRoot\system32\conhost.exe $conhostFlags $scriptsDir\OpenWebSearch.cmd"

    Write-Log "Configuring registry entries for Edge protocol redirect"
    reg.exe add "HKCR\microsoft-edge" /f /ve /d "URL:microsoft-edge" 2>&1 | Out-Null
    reg.exe add "HKCR\microsoft-edge" /f /v "URL Protocol" /d `"`" 2>&1 | Out-Null
    reg.exe add "HKCR\microsoft-edge" /f /v "NoOpenWith" /d `"`" 2>&1 | Out-Null
    reg.exe add "HKCR\microsoft-edge\shell\open\command" /f /ve /d "$stubTargetPath %1" 2>&1 | Out-Null
    reg.exe add "HKCR\MSEdgeHTM" /f /v "NoOpenWith" /d `"`" 2>&1 | Out-Null
    reg.exe add "HKCR\MSEdgeHTM\shell\open\command" /f /ve /d "$stubTargetPath %1" 2>&1 | Out-Null
    reg.exe add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\ie_to_edge_stub.exe" /f /v UseFilter /d 1 /t reg_dword 2>&1 | Out-Null
    reg.exe add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\ie_to_edge_stub.exe\0" /f /v FilterFullPath /d "$stubTargetPath" 2>&1 | Out-Null
    reg.exe add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\ie_to_edge_stub.exe\0" /f /v Debugger /d "$conhostDebugger" 2>&1 | Out-Null
    reg.exe delete "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\msedge.exe" /f 2>&1 | Out-Null

    $repairContent = @"
`$stubPath = "$stubTargetPath"
`$owsPath = "$openWebSearchPath"
if (-not (Test-Path `$stubPath)) { exit }
if (-not (Test-Path `$owsPath)) { exit }
`$cmd = (Get-ItemProperty "Registry::HKEY_CLASSES_ROOT\microsoft-edge\shell\open\command" -ErrorAction SilentlyContinue).'(default)'
if (`$cmd -and `$cmd -notlike "*ie_to_edge_stub*") {
    reg.exe add "HKCR\microsoft-edge\shell\open\command" /f /ve /d "`$stubPath %1" 2>&1 | Out-Null
    reg.exe add "HKCR\MSEdgeHTM\shell\open\command" /f /ve /d "`$stubPath %1" 2>&1 | Out-Null
}
"@
    $repairScriptPath = "$scriptsDir\OpenWebSearchRepair.ps1"
    $repairContent | Out-File -FilePath $repairScriptPath -Encoding UTF8 -Force

    $repairTaskName = "OpenWebSearchRepair"
    $repairAction = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -NoProfile -Command `"iex([IO.File]::ReadAllText('$repairScriptPath'))`""
    $repairTrigger = New-ScheduledTaskTrigger -AtLogon
    $repairSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
    $repairPrincipal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName $repairTaskName -TaskPath "\EvolveOS\" -Action $repairAction -Trigger $repairTrigger -Settings $repairSettings -Principal $repairPrincipal -Force | Out-Null
}

function Remove-ChromiumEdge {
    Write-Log "Starting Edge Chromium uninstallation process"
    $edgePath = "$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe"
    New-Item -Path $edgePath -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
    New-Item -Path $edgePath -ItemType File -Name "MicrosoftEdge.exe" -ErrorAction SilentlyContinue | Out-Null
    
    $uninstallKeys = Get-ChildItem "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    $edgeUninstallCount = 0
    foreach ($key in $uninstallKeys) {
        $displayName = (Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue).DisplayName
        if ($displayName -like "*Microsoft Edge*") {
            $uninstallString = (Get-ItemProperty $key.PSPath).UninstallString
            if ($uninstallString) {
                $edgeUninstallCount++
                Stop-EdgeProcesses
                if ($uninstallString -like "*msiexec*") {
                    Start-Process cmd.exe "/c $uninstallString /quiet" -WindowStyle Hidden -Wait | Out-Null
                } else {
                    Start-Process cmd.exe "/c $uninstallString --force-uninstall --silent" -WindowStyle Hidden -Wait | Out-Null
                }
            }
        }
    }
    
    Get-AppxPackage -AllUsers Microsoft.MicrosoftEdge.Stable | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue | Out-Null
    Remove-Item -Recurse -Force $edgePath -ErrorAction SilentlyContinue | Out-Null

    Write-Log "Starting EdgeUpdate removal process"
    $edgeupdate = @()
    $searchPaths = @("LocalApplicationData", "ProgramFilesX86", "ProgramFiles")
    foreach ($pathType in $searchPaths) {
        $folder = [Environment]::GetFolderPath($pathType)
        $searchPattern = "$folder\Microsoft\EdgeUpdate\*.*.*.*\MicrosoftEdgeUpdate.exe"
        $foundFiles = Get-ChildItem $searchPattern -Recurse -ErrorAction SilentlyContinue
        if ($foundFiles) { $edgeupdate += $foundFiles.FullName }
    }
    
    $backupRegFile = "$env:TEMP\EdgeUpdate_ClientState_Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').reg"
    $clientStatePath = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\ClientState"
    if (Test-Path $clientStatePath) {
        cmd /c "reg export `"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\ClientState`" `"$backupRegFile`" /y" 2>$null
    }
    
    foreach ($path in $edgeupdate) {
        if (Test-Path $path) {
            Start-Process -FilePath $path -ArgumentList "/unregsvc" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
            $waitCount = 0
            do {
                Start-Sleep 3
                $runningProcesses = Get-Process -Name "setup", "MicrosoftEdge*" -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "*\Microsoft\Edge*" }
            } while ($runningProcesses -and $waitCount++ -lt 20)
            if (Test-Path $path) {
                Start-Process -FilePath $path -ArgumentList "/uninstall" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
            }
        }
    }
    
    if ((Test-Path $backupRegFile)) {
        cmd /c "reg import `"$backupRegFile`"" 2>$null
        Remove-Item $backupRegFile -ErrorAction SilentlyContinue
    }
}

function Remove-EdgeRegistryKeys {
    Write-Log "Starting comprehensive Edge registry cleanup"
    $directPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Edge", "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Edge",
        "HKCU:\Software\Microsoft\Edge", "HKCU:\Software\Microsoft\EdgeUpdate",
        "HKLM:\SOFTWARE\Clients\StartMenuInternet\Microsoft Edge",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MicrosoftEdge",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge",
        "HKLM:\SYSTEM\CurrentControlSet\Services\Eventlog\Application\Edge",
        "HKLM:\SYSTEM\CurrentControlSet\Services\Eventlog\Application\edgeupdate",
        "HKLM:\SYSTEM\CurrentControlSet\Services\Eventlog\Application\edgeupdatem"
    )
    foreach ($path in $directPaths) { if (Test-Path $path) { Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue } }

    $valuesToRemove = @(
        @{Path = "HKLM:\SOFTWARE\RegisteredApplications"; Name = "Microsoft Edge"},
        @{Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\AppLaunch"; Name = "MSEdge"},
        @{Path = "HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store"; Name = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"}
    )
    foreach ($item in $valuesToRemove) {
        if ((Test-Path $item.Path) -and (Get-ItemProperty -Path $item.Path -Name $item.Name -ErrorAction SilentlyContinue)) {
            Remove-ItemProperty -Path $item.Path -Name $item.Name -Force -ErrorAction SilentlyContinue
        }
    }

    $patterns = @(
        @{Root = "HKLM:\SOFTWARE\Classes"; Pattern = "microsoft-edge"},
        @{Root = "HKLM:\SOFTWARE\Classes"; Pattern = "MicrosoftEdgeUpdate*"},
        @{Root = "HKLM:\SOFTWARE\Classes"; Pattern = "MSEdge*"},
        @{Root = "HKLM:\SOFTWARE\Classes\WOW6432Node"; Pattern = "MicrosoftEdgeUpdate*"},
        @{Root = "HKLM:\SOFTWARE\WOW6432Node\Classes"; Pattern = "MicrosoftEdgeUpdate*"}
    )
    foreach ($patternItem in $patterns) {
        if (Test-Path $patternItem.Root) {
            $matchedKeys = Get-ChildItem -Path $patternItem.Root -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like $patternItem.Pattern }
            foreach ($key in $matchedKeys) { Remove-Item $key.PSPath -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }

    $muiCachePath = "HKCU:\SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache"
    if (Test-Path $muiCachePath) {
        $properties = Get-ItemProperty -Path $muiCachePath -ErrorAction SilentlyContinue
        if ($properties) {
            foreach ($prop in $properties.PSObject.Properties) {
                if ($prop.Name -like "*Edge*" -or $prop.Name -like "*EdgeUpdate*") {
                    Remove-ItemProperty -Path $muiCachePath -Name $prop.Name -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }
}

function Remove-AdditionalEdgeFolders {
    $systemPaths = @("C:\ProgramData\Microsoft\EdgeUpdate", "C:\Windows\Temp\MsEdgeCrashpad")
    foreach ($path in $systemPaths) { if (Test-Path $path) { Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue } }
    $userProfiles = Get-ChildItem -Path "C:\Users" -Directory -ErrorAction SilentlyContinue | Where-Object { Test-Path "$($_.FullName)\NTUSER.DAT" }
    foreach ($profile in $userProfiles) {
        $edgeLocalPath = "$($profile.FullName)\AppData\Local\Microsoft\Edge"
        if (Test-Path $edgeLocalPath) { Remove-Item $edgeLocalPath -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Write-Log "Checking for Edge installations..."
$legacyInstalled = Test-LegacyEdgeInstalled
$chromiumInstalled = Test-ChromiumEdgeInstalled
$removedSomething = $false
$stubPath = $null

if ($chromiumInstalled) {
    $stubLocations = @("$env:ProgramData\ie_to_edge_stub.exe", "$env:Public\ie_to_edge_stub.exe")
    foreach ($loc in $stubLocations) { if (Test-Path $loc) { $stubPath = $loc; break } }
    if (!$stubPath) {
        $stubSearch = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft\Edge" -Filter "ie_to_edge_stub.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($stubSearch) { $stubPath = $stubSearch.FullName }
    }
    if ($stubPath) {
        $scriptsDir = "C:\ProgramData\EvolveOS\OpenWebSearch"
        New-Item -ItemType Directory -Path $scriptsDir -Force -ErrorAction SilentlyContinue | Out-Null
        Copy-Item $stubPath "$scriptsDir\ie_to_edge_stub.exe" -Force -ErrorAction SilentlyContinue
    }
}

if ($legacyInstalled) { Stop-EdgeProcesses; Remove-LegacyEdge; $removedSomething = $true }
if ($chromiumInstalled) { Stop-EdgeProcesses; Remove-ChromiumEdge; $removedSomething = $true }

if ($removedSomething) {
    $edgeFolders = Get-ChildItem -Path "$env:SystemDrive\Program Files (x86)\Microsoft" -Directory -ErrorAction SilentlyContinue | Where-Object { ($_.Name -like "*Edge*" -or $_.Name -like "*Temp*") -and $_.Name -notlike "*EdgeWebView*" }
    if ($edgeFolders) { $edgeFolders | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }
    Remove-EdgeShortcuts
    Remove-EdgeRegistryKeys
    Remove-AdditionalEdgeFolders
}

Install-EdgeProtocolRedirect

try {
    $edgeTasks = Get-ScheduledTask -TaskName "*Edge*" -ErrorAction SilentlyContinue
    if ($edgeTasks) {
        foreach ($task in $edgeTasks) {
            if ($task.TaskName -eq "EdgeRemoval") { continue }
            Unregister-ScheduledTask -TaskName $task.TaskName -TaskPath $task.TaskPath -Confirm:$false -ErrorAction SilentlyContinue
        }
    }
} catch { }

Write-Log "Done."
""";

        public static readonly string OneDriveRemovalScript = """
<#
  .SYNOPSIS
      Removes Microsoft OneDrive from Windows 10/11 systems.
#>
$logFolder = "C:\ProgramData\EvolveOS\Logs"
$logFile = "$logFolder\OneDriveRemovalLog.txt"
if (!(Test-Path $logFolder)) { New-Item -ItemType Directory -Path $logFolder -Force | Out-Null }
function Write-Log { param ([string]$Message) "$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')) - $Message" | Out-File -FilePath $logFile -Append; Write-Host $Message }

function Schedule-DeleteOnReboot {
    param([string]$Path)
    $code = '[DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] public static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);'
    if (-not ([System.Management.Automation.PSTypeName]'Win32.Kernel32').Type) { Add-Type -MemberDefinition $code -Name 'Kernel32' -Namespace 'Win32' -ErrorAction SilentlyContinue }
    return [Win32.Kernel32]::MoveFileEx($Path, $null, 4)
}

Write-Log "Starting OneDrive removal process"

function Get-TargetUser {
    try {
        $user = Get-WmiObject Win32_ComputerSystem | Select-Object -ExpandProperty UserName
        if ($user -and $user -ne "NT AUTHORITY\SYSTEM") { return $user.Split('\')[1] }
    } catch { }
    try {
        $explorer = Get-Process explorer -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($explorer) { return $explorer.GetOwner().User }
    } catch { }
    return $null
}

function Get-UserSID {
    param($Username)
    try {
        $profListPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList'
        foreach ($key in Get-ChildItem $profListPath -ErrorAction SilentlyContinue) {
            $profPath = (Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue).ProfileImagePath
            if ($profPath -and $profPath.EndsWith("\$Username")) { return $key.PSChildName }
        }
        return $null
    } catch { return $null }
}

if ($env:USERNAME -eq "SYSTEM" -or $env:USERNAME -like "*$" -or $env:USERPROFILE -like "*\system32\config\systemprofile") {
    $targetUser = Get-TargetUser
    if ($targetUser) { $userProfilePath = "C:\Users\$targetUser" } else { $userProfilePath = $null }
} else {
    $targetUser = $env:USERNAME
    $userProfilePath = $env:USERPROFILE
}

Write-Log "Removing OneDrive AppxPackage"
try { Get-AppxPackage -AllUsers *OneDriveSync* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue } catch { }

$uninstallExecuted = $false
$hklmUninstallKey = "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe"

try {
    $uninstallString = reg.exe query $hklmUninstallKey /v UninstallString 2>$null
    if ($LASTEXITCODE -eq 0 -and $uninstallString) {
        $uninstallLine = $uninstallString | Where-Object { $_ -match "UninstallString" } | Select-Object -First 1
        if ($uninstallLine -match "REG_SZ\s+(.+)") {
            $uninstallCommand = $matches[1].Trim()
            Stop-Process -Name "*OneDrive*" -Force -ErrorAction SilentlyContinue | Out-Null
            if ($uninstallCommand -match '^"([^"]+)"(.*)') {
                Start-Process -FilePath $matches[1] -ArgumentList $matches[2].Trim() -WindowStyle Hidden -Wait | Out-Null
            } else { cmd.exe /c $uninstallCommand 2>&1 | Out-Null }
            $uninstallExecuted = $true
        }
    }
} catch { }

if (-not $uninstallExecuted -and $targetUser) {
    $userSID = Get-UserSID -Username $targetUser
    if ($userSID) {
        $uninstallKey = "HKU\$userSID\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe"
        try {
            $uninstallString = reg.exe query $uninstallKey /v UninstallString 2>$null
            if ($LASTEXITCODE -eq 0 -and $uninstallString) {
                $uninstallLine = $uninstallString | Where-Object { $_ -match "UninstallString" } | Select-Object -First 1
                if ($uninstallLine -match "REG_SZ\s+(.+)") {
                    $uninstallCommand = $matches[1].Trim()
                    Stop-Process -Name "*OneDrive*" -Force -ErrorAction SilentlyContinue | Out-Null
                    if ($uninstallCommand -match '^"([^"]+)"(.*)') {
                        Start-Process -FilePath $matches[1] -ArgumentList $matches[2].Trim() -WindowStyle Hidden -Wait | Out-Null
                    } else { cmd.exe /c $uninstallCommand 2>&1 | Out-Null }
                }
            }
        } catch { }
    }
}

reg.exe delete $hklmUninstallKey /f 2>&1 | Out-Null
if ($targetUser -and $userSID) { reg.exe delete "HKU\$userSID\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe" /f 2>&1 | Out-Null }

$systemRegistryPaths = @("HKLM\SOFTWARE\Microsoft\OneDrive", "HKLM\SOFTWARE\WOW6432Node\Microsoft\OneDrive")
foreach ($path in $systemRegistryPaths) { reg.exe delete $path /f 2>&1 | Out-Null }

if ($targetUser) {
    if (-not $userSID) { $userSID = Get-UserSID -Username $targetUser }
    if ($userSID) {
        $userRegistryPaths = @("HKU\$userSID\SOFTWARE\Microsoft\OneDrive", "HKU\$userSID\SOFTWARE\WOW6432Node\Microsoft\OneDrive")
        foreach ($path in $userRegistryPaths) { reg.exe delete $path /f 2>&1 | Out-Null }
    }
}

if ($userProfilePath) {
    $currentUserOneDrivePath = Join-Path $userProfilePath "AppData\Local\Microsoft\OneDrive"
    if (Test-Path $currentUserOneDrivePath) {
        try {
            takeown /f $currentUserOneDrivePath /r /d y 2>&1 | Out-Null
            icacls $currentUserOneDrivePath /grant "${env:USERNAME}:F" /t 2>&1 | Out-Null
            Remove-Item $currentUserOneDrivePath -Recurse -Force -ErrorAction SilentlyContinue
        } catch { }
    }
}

if ($userProfilePath) {
    $startMenuPath = Join-Path $userProfilePath "AppData\Roaming\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk"
    if (Test-Path $startMenuPath) { Remove-Item $startMenuPath -Force -ErrorAction SilentlyContinue }
}

if (Test-Path "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk") {
    Remove-Item "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk" -Force -ErrorAction SilentlyContinue
}

$systemPaths = @("C:\Windows\System32\OneDriveSetup.exe", "C:\Windows\SysWOW64\OneDriveSetup.exe", "C:\Program Files\Microsoft OneDrive", "C:\ProgramData\Microsoft OneDrive")
foreach ($path in $systemPaths) {
    if (Test-Path $path) {
        if (Test-Path $path -PathType Container) {
            takeown /f $path /r /d y 2>&1 | Out-Null
            icacls $path /grant "${env:USERNAME}:F" /t 2>&1 | Out-Null
        } else {
            takeown /f $path 2>&1 | Out-Null
            icacls $path /grant "${env:USERNAME}:F" 2>&1 | Out-Null
        }
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$remaining = $systemPaths | Where-Object { Test-Path $_ }
if ($remaining) {
    foreach ($path in $remaining) {
        if (Test-Path $path -PathType Container) {
            Get-ChildItem $path -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object { Schedule-DeleteOnReboot $_.FullName | Out-Null }
        } else { Schedule-DeleteOnReboot $path | Out-Null }
    }
}

try {
    $oneDriveTasks = Get-ScheduledTask -TaskName "*OneDrive*" -ErrorAction SilentlyContinue
    if ($oneDriveTasks) {
        foreach ($task in $oneDriveTasks) {
            if ($task.TaskName -eq "OneDriveRemoval") { continue }
            Unregister-ScheduledTask -TaskName $task.TaskName -TaskPath $task.TaskPath -Confirm:$false -ErrorAction SilentlyContinue
        }
    }
} catch { }

$markerKey = "HKLM\SOFTWARE\EvolveOS\OneDriveRemoval"
$markerValue = reg.exe query $markerKey /v "DefaultUserConfigured" 2>$null

if ($LASTEXITCODE -ne 0) {
    reg.exe Load HKEY_USERS\Default "C:\Users\Default\NTUSER.DAT" 2>&1 | Out-Null
    reg.exe delete "HKU\Default\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v "OneDriveSetup" /f 2>&1 | Out-Null
    reg.exe add "HKU\Default\SOFTWARE\Microsoft\OneDrive" /v "EnableTHDFFeatures" /t REG_DWORD /d "0" /f 2>&1 | Out-Null
    Stop-Process -Name "regedit" -Force -ErrorAction SilentlyContinue
    reg.exe Unload HKEY_USERS\Default 2>&1 | Out-Null
    reg.exe add $markerKey /v "DefaultUserConfigured" /t REG_SZ /d "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" /f 2>&1 | Out-Null
}

Write-Log "Done."
""";
    }
}