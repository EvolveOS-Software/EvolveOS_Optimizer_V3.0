// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Provides methods for determining the specific version, build, and edition of the underlying Windows operating system.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responsibility:</b>
/// This service acts as the source of truth for OS versioning. Because Windows 10 and Windows 11 
/// technically share the same Major/Minor kernel version (10.0), standard .NET APIs are often insufficient 
/// for proper differentiation.
/// </para>
/// <para>
/// <b>Mechanism:</b>
/// It queries <c>Environment.OSVersion</c> for base build numbers and falls back to reading the Registry 
/// (<c>HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion</c>) to extract the exact Update Build Revision (UBR) 
/// and explicit Product Name. This precise data is critical for the compatibility filters to accurately gate settings.
/// </para>
/// </remarks>
public class WindowsVersionService : IWindowsVersionService
{
    #region Fields & Constructor

    private readonly ILogService _logService;

    public WindowsVersionService(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    #endregion

    #region Build & Revision Detection

    public int GetWindowsBuildNumber()
    {
        try
        {
            return Environment.OSVersion.Version.Build;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error getting Windows build number", ex);
            return 0;
        }
    }

    public int GetWindowsBuildRevision()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var ubr = key?.GetValue("UBR");
            if (ubr is int ubrValue)
                return ubrValue;
            return 0;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error getting Windows build revision (UBR)", ex);
            return 0;
        }
    }

    #endregion

    #region Edition & Generation Detection

    public bool IsWindows11()
    {
        try
        {
            var os = Environment.OSVersion;
            if (os.Version.Major != 10) return false;

            if (os.Version.Build >= 22000) return true;

            using var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");
            var productName = key?.GetValue("ProductName")?.ToString() ?? "";
            return productName.IndexOf("Windows 11", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error detecting Windows 11", ex);
            return false;
        }
    }

    public bool IsWindowsServer()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");
            var productName = key?.GetValue("ProductName")?.ToString() ?? "";
            return productName.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error detecting Windows Server", ex);
            return false;
        }
    }

    #endregion
}