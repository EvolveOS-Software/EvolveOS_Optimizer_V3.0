# ==============================================================================
# Sparse Package Builder & Signer for EvolveOS Optimizer
#
# Run Powershell as Administrator
# goto script path
# Add compiled EvolveOS_Optimizer.exe and EvolveOS_MenuProxy.dll
# Run Command : Set-ExecutionPolicy Bypass -Scope Process -Force; .\Build-SparsePackage.ps1
#
# Files Created by this script:
# - EvolveOS_Package.msix   (Add to VS as Embedded Resource)
# - EvolveOS_DevCert.cer    (Add to VS as Embedded Resource)
# - AppxManifest.xml        (Add to VS as Embedded Resource)
# - EvolveOS_DevCert.pfx    (Private key - Keep in folder, do NOT embed)
#
# Add the generated .msix, .cer, and .xml files to your Visual Studio resources folder!
#
# Removal of existing package (for clean testing):
# run command 1 : Get-AppxPackage -Name "EvolveOS.Optimizer" | Remove-AppxPackage
# run command 2 : taskkill /f /im explorer.exe; start explorer.exe
# Remove - EvolveOS_MenuProxy.dll
#        - EvolveOS_Package.msix
#        - AppxManifest.xml
# ==============================================================================

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrWhiteSpace($ScriptDir)) { $ScriptDir = $PWD.Path }
Set-Location $ScriptDir
[Environment]::CurrentDirectory = $ScriptDir

if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "You must run this PowerShell window as Administrator!"
    Read-Host "Press Enter to exit..."
    exit
}

$AppName = "EvolveOS_Optimizer"
$Publisher = "CN=EvolveOS"
$MsixPath = "$ScriptDir\EvolveOS_Package.msix"
$CertPathPfx = "$ScriptDir\EvolveOS_DevCert.pfx"
$CertPathCer = "$ScriptDir\EvolveOS_DevCert.cer"
$CertPassword = "password123"

# 1. DYNAMICALLY PULL VERSION FROM COMPILED EXE
$TargetExePath = "$ScriptDir\$AppName.exe"
$AppVersion = "1.0.0.0" 

if (Test-Path $TargetExePath) {
    $VersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($TargetExePath)
    $RawVersion = $VersionInfo.ProductVersion
    
    if ([string]::IsNullOrWhiteSpace($RawVersion) -or $RawVersion -eq "0.0.0.0") {
        $RawVersion = $VersionInfo.FileVersion
    }

    if ($RawVersion -match '(\d+(\.\d+){1,3})') {
        $CleanVersion = $matches[0]
        try {
            $Parsed = [Version]$CleanVersion
            $Build = if ($Parsed.Build -ne -1) { $Parsed.Build } else { 0 }
            $Rev = if ($Parsed.Revision -ne -1) { $Parsed.Revision } else { 0 }
            $AppVersion = "$($Parsed.Major).$($Parsed.Minor).$Build.$Rev"
        } catch {
            $AppVersion = "1.0.0.0"
        }
    }
}

Write-Host "Syncing MSIX version with App Version: $AppVersion" -ForegroundColor Green

# 2. HUNT FOR THE SDK (Includes D: Drive paths)
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

# 3. Set up the package directory using Absolute Paths
$PackageDir = "$ScriptDir\SparsePackageTemp"
if (Test-Path $PackageDir) { Remove-Item $PackageDir -Recurse -Force }
New-Item -ItemType Directory -Path "$PackageDir\Assets" | Out-Null

# 4. Generate dummy assets AND a dummy Executable
$Base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="
$PngBytes = [Convert]::FromBase64String($Base64Png)
[IO.File]::WriteAllBytes("$PackageDir\Assets\EvolveOS_Optimizer-Logo.png", $PngBytes)
Set-Content -Path "$PackageDir\$AppName.exe" -Value "DummyExeForMakeAppxValidation"

# Bundle the compiled C++ DLL into the package!
if (Test-Path "$ScriptDir\EvolveOS_MenuProxy.dll") {
    Copy-Item -Path "$ScriptDir\EvolveOS_MenuProxy.dll" -Destination "$PackageDir\EvolveOS_MenuProxy.dll" -Force
} else {
    Write-Warning "EvolveOS_MenuProxy.dll not found in $ScriptDir! Your context menu will not work without it."
}

# 5. Generate the AppxManifest.xml
$ManifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
         xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
         xmlns:com="http://schemas.microsoft.com/appx/manifest/com/windows10"
         xmlns:desktop4="http://schemas.microsoft.com/appx/manifest/desktop/windows10/4"
         xmlns:desktop5="http://schemas.microsoft.com/appx/manifest/desktop/windows10/5"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         IgnorableNamespaces="uap uap3 uap10 com desktop4 desktop5 rescap">
  
  <Identity Name="EvolveOS.Optimizer" Publisher="$Publisher" Version="$AppVersion" ProcessorArchitecture="x64" />
  
  <Properties>
    <DisplayName>EvolveOS Optimizer</DisplayName>
    <PublisherDisplayName>EvolveOS Software</PublisherDisplayName>
    <Logo>Assets\EvolveOS_Optimizer-Logo.png</Logo>
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
        
        <!-- COM SERVER DECLARATION -->
        <com:Extension Category="windows.comServer">
          <com:ComServer>
            <com:SurrogateServer DisplayName="EvolveOS Context Menu">
              <!-- Configured for your exact DLL and CLSID -->
              <com:Class Id="24e9458d-6c4c-44cd-a2f1-2ac32a7dec73" Path="EvolveOS_MenuProxy.dll" ThreadingModel="STA"/>
            </com:SurrogateServer>
          </com:ComServer>
        </com:Extension>

        <!-- MODERN CONTEXT MENU DECLARATION -->
        <desktop4:Extension Category="windows.fileExplorerContextMenus">
          <desktop4:FileExplorerContextMenus>
            <!-- Target Files (Uses desktop4) -->
            <desktop4:ItemType Type="*">
              <desktop4:Verb Id="EvolveOSMenuFiles" Clsid="24e9458d-6c4c-44cd-a2f1-2ac32a7dec73" />
            </desktop4:ItemType>
            <!-- Target Folders (Uses desktop5) -->
            <desktop5:ItemType Type="Directory">
              <desktop5:Verb Id="EvolveOSMenuFolders" Clsid="24e9458d-6c4c-44cd-a2f1-2ac32a7dec73" />
            </desktop5:ItemType>
            <!-- Target Folder Background/Desktop (Uses desktop5) -->
            <desktop5:ItemType Type="Directory\Background">
              <desktop5:Verb Id="EvolveOSMenuBackground" Clsid="24e9458d-6c4c-44cd-a2f1-2ac32a7dec73" />
            </desktop5:ItemType>
          </desktop4:FileExplorerContextMenus>
        </desktop4:Extension>

      </Extensions>
    </Application>
  </Applications>
</Package>
"@
Set-Content -Path "$PackageDir\AppxManifest.xml" -Value $ManifestContent

# 6. Create a Self-Signed Code Signing Certificate AND export the .cer file automatically
$PasswordSecure = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText

if (!(Test-Path $CertPathPfx)) {
    Write-Host "Generating Self-Signed Certificate..." -ForegroundColor Cyan
    $Cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature -FriendlyName "EvolveOS Dev Cert" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
    
    # Export PFX (For signing the MSIX)
    Export-PfxCertificate -Cert $Cert -FilePath $CertPathPfx -Password $PasswordSecure | Out-Null
    
    # Export CER (For embedding in your C# App so it can install it)
    Export-Certificate -Cert $Cert -FilePath $CertPathCer | Out-Null
    
    Write-Host "Adding cert to Trusted People..." -ForegroundColor Cyan
    Import-PfxCertificate -FilePath $CertPathPfx -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" -Password $PasswordSecure | Out-Null
} else {
    # If PFX exists but CER is missing, generate the CER from the PFX
    if (!(Test-Path $CertPathCer)) {
        Write-Host "Exporting .cer file from existing .pfx..." -ForegroundColor Cyan
        $Cert = Get-PfxCertificate -FilePath $CertPathPfx
        Export-Certificate -Cert $Cert -FilePath $CertPathCer | Out-Null
    }
}

# 7. Pack the directory into an MSIX
Write-Host "Packing MSIX..." -ForegroundColor Cyan
if (Test-Path $MsixPath) { Remove-Item $MsixPath -Force }
& $MakeAppx pack /d $PackageDir /p $MsixPath

# 8. Sign the MSIX
Write-Host "Signing MSIX..." -ForegroundColor Cyan
& $SignTool sign /fd SHA256 /a /f $CertPathPfx /p $CertPassword $MsixPath

# 9. Clean up and Export Standalone Files
Write-Host "Exporting standalone AppxManifest.xml..." -ForegroundColor Cyan
Copy-Item "$PackageDir\AppxManifest.xml" -Destination "$ScriptDir\AppxManifest.xml" -Force

Remove-Item $PackageDir -Recurse -Force
Write-Host "`nSUCCESS! Your fully bundled EvolveOS_Package.msix, EvolveOS_DevCert.cer, and AppxManifest.xml are ready!" -ForegroundColor Green