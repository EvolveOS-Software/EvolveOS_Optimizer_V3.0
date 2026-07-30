# ==============================================================================
# Sparse Package Builder & Signer for EvolveOS Optimizer
#
# Run Powershell as Administrator
# goto script path
# Add compiled EvolveOS_Optimizer.exe
# Run Command : Set-ExecutionPolicy Bypass -Scope Process -Force; .\Build-SparsePackage.ps1
# Run Command : $Cert = Get-PfxCertificate -FilePath ".\EvolveOS_DevCert.pfx"; Export-Certificate -Cert $Cert -FilePath ".\EvolveOS_DevCert.cer"
# Add .msix and cer file to the resource folder and publish the application
#
# To uninstall the .msix run command : Get-AppxPackage *EvolveOS.Optimizer* | Remove-AppxPackage -AllUsers
# ==============================================================================

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrWhiteSpace($ScriptDir)) { $ScriptDir = "E:\EvolveOS_Optimizer_V3.0\Assets" }
Set-Location $ScriptDir
[Environment]::CurrentDirectory = $ScriptDir

if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "You must run this PowerShell window as Administrator!"
    Read-Host "Press Enter to exit..."
    exit
}

$AppName = "EvolveOS_Optimizer"
$Publisher = "CN=EvolveOS"
$MsixPath = "$ScriptDir\EvolveOS_Lighting.msix"
$CertPath = "$ScriptDir\EvolveOS_DevCert.pfx"
$CertPassword = "password123"

# 1. 🚀 DYNAMICALLY PULL VERSION FROM COMPILED EXE
$TargetExePath = "$ScriptDir\$AppName.exe"
$AppVersion = "1.0.0.0" # Fallback version

if (Test-Path $TargetExePath) {
    $VersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($TargetExePath)
    $RawVersion = $VersionInfo.ProductVersion
    
    if ([string]::IsNullOrWhiteSpace($RawVersion) -or $RawVersion -eq "0.0.0.0") {
        $RawVersion = $VersionInfo.FileVersion
    }

    # 🚀 USE REGEX TO EXTRACT ONLY THE NUMBERS (e.g., "Build: 1.2.0.382" -> "1.2.0.382")
    if ($RawVersion -match '(\d+(\.\d+){1,3})') {
        $CleanVersion = $matches[0]
        
        # Format correction: MSIX strictly requires Major.Minor.Build.Revision (4 parts)
        try {
            $Parsed = [Version]$CleanVersion
            $Build = if ($Parsed.Build -ne -1) { $Parsed.Build } else { 0 }
            $Rev = if ($Parsed.Revision -ne -1) { $Parsed.Revision } else { 0 }
            $AppVersion = "$($Parsed.Major).$($Parsed.Minor).$Build.$Rev"
        } catch {
            Write-Warning "Could not parse extracted version ('$CleanVersion'). Defaulting to 1.0.0.0"
            $AppVersion = "1.0.0.0"
        }
    } else {
        Write-Warning "No version numbers found in ('$RawVersion'). Defaulting to 1.0.0.0"
    }
} else {
    Write-Warning "Could not find $AppName.exe in $ScriptDir!"
    Write-Warning "Proceeding with fallback version $AppVersion."
}

Write-Host "Syncing MSIX version with App Version: $AppVersion" -ForegroundColor Green

# 2. HUNT FOR THE SDK
$SearchBases = @("D:\Windows Kits\10\bin", "C:\Program Files (x86)\Windows Kits\10\bin", "D:\Program Files (x86)\Windows Kits\10\bin", "E:\Program Files (x86)\Windows Kits\10\bin", "C:\Windows Kits\10\bin")
$SdkPath = $null

foreach ($base in $SearchBases) {
    if (Test-Path $base) {
        $versions = Get-ChildItem -Path $base -Filter "10.0.*" -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending
        foreach ($ver in $versions) {
            $testPath = "$($ver.FullName)\x64"
            if ((Test-Path "$testPath\makeappx.exe") -and (Test-Path "$testPath\signtool.exe")) {
                $SdkPath = $testPath
                break
            }
        }
    }
    if ($SdkPath) { break }
}

if (!$SdkPath) { 
    Write-Error "Windows SDK tools not found!"
    Read-Host "Press Enter to exit..."
    exit 
}

$MakeAppx = "$SdkPath\makeappx.exe"
$SignTool = "$SdkPath\signtool.exe"
Write-Host "Found SDK tools at: $SdkPath" -ForegroundColor Cyan

# 3. Set up the package directory using Absolute Paths
$PackageDir = "$ScriptDir\SparsePackageTemp"
if (Test-Path $PackageDir) { Remove-Item $PackageDir -Recurse -Force }
New-Item -ItemType Directory -Path "$PackageDir\Assets" | Out-Null

# 4. Generate dummy assets AND a dummy Executable so MakeAppx passes validation
$Base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="
$PngBytes = [Convert]::FromBase64String($Base64Png)
[IO.File]::WriteAllBytes("$PackageDir\Assets\EvolveOS_Optimizer-Logo.png", $PngBytes)

# Create a fake EXE file to satisfy MakeAppx. The real EXE is used at runtime.
Set-Content -Path "$PackageDir\$AppName.exe" -Value "DummyExeForMakeAppxValidation"

# 5. Generate the AppxManifest.xml using the injected $AppVersion variable
$ManifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
         xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         IgnorableNamespaces="uap uap3 uap10 rescap">
  
  <Identity Name="EvolveOS.Optimizer" Publisher="$Publisher" Version="$AppVersion" ProcessorArchitecture="x64" />
  
  <Properties>
    <DisplayName>EvolveOS Optimizer</DisplayName>
    <PublisherDisplayName>EvolveOS Software</PublisherDisplayName>
    <Logo>Assets\EvolveOS_Optimizer-Logo.png</Logo>
    <!-- 🚀 ALLOW EXTERNAL LOCATION CONTENT -->
    <uap10:AllowExternalContent>true</uap10:AllowExternalContent>
  </Properties>
  
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22621.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
    <rescap:Capability Name="unvirtualizedResources"/>
  </Capabilities>
  
  <Applications>
    <Application Id="EvolveOSOptimizer" Executable="$AppName.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="EvolveOS Optimizer" Description="System Optimizer" 
                          Square150x150Logo="Assets\EvolveOS_Optimizer-Logo.png" 
                          Square44x44Logo="Assets\EvolveOS_Optimizer-Logo.png" 
                          BackgroundColor="transparent" />
      <Extensions>
        <uap3:Extension Category="windows.appExtension">
          <uap3:AppExtension Name="com.microsoft.windows.lighting" Id="EvolveOSOptimizer" DisplayName="EvolveOS Lighting Profile" PublicFolder="Assets" />
        </uap3:Extension>
      </Extensions>
    </Application>
  </Applications>
</Package>
"@
Set-Content -Path "$PackageDir\AppxManifest.xml" -Value $ManifestContent

# 6. Create a Self-Signed Code Signing Certificate
$PasswordSecure = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText

if (!(Test-Path $CertPath)) {
    Write-Host "Generating Self-Signed Certificate..." -ForegroundColor Cyan
    $Cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature -FriendlyName "EvolveOS Dev Cert" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
    Export-PfxCertificate -Cert $Cert -FilePath $CertPath -Password $PasswordSecure | Out-Null
    
    Write-Host "Adding cert to Trusted People..." -ForegroundColor Cyan
    Import-PfxCertificate -FilePath $CertPath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" -Password $PasswordSecure | Out-Null
}

# 7. Pack the directory into an MSIX
Write-Host "Packing MSIX..." -ForegroundColor Cyan
if (Test-Path $MsixPath) { Remove-Item $MsixPath -Force }
& $MakeAppx pack /d $PackageDir /p $MsixPath

# 8. Sign the MSIX
Write-Host "Signing MSIX..." -ForegroundColor Cyan
& $SignTool sign /fd SHA256 /a /f $CertPath /p $CertPassword $MsixPath

# 9. Clean up
Remove-Item $PackageDir -Recurse -Force
Write-Host "`nSUCCESS! EvolveOS_Lighting.msix is now located in $ScriptDir" -ForegroundColor Green

Read-Host "`nPress Enter to exit..."