// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Tweaks;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Managers;

public static class AppManager
{
    private static readonly string IconCacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "Icons");

    public static async Task<List<SystemAppItem>> GetInstalledApps(bool uninstallableOnly)
    {
        if (!Directory.Exists(IconCacheDirectory))
        {
            Directory.CreateDirectory(IconCacheDirectory);
        }

        var largeIcons = new IntPtr[1];

        Win32Helper.ExtractIconEx(@"C:\Windows\System32\imageres.dll", 152, largeIcons, new IntPtr[0], 1);

        var hIcon = largeIcons[0];

        if (hIcon != IntPtr.Zero)
        {
            try
            {
                using var clonedIcon = (Icon)Icon.FromHandle(hIcon).Clone();
                using var bmp = clonedIcon.ToBitmap();
                bmp.Save(Path.Combine(IconCacheDirectory, "defaulticon.png"), ImageFormat.Png);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            finally
            {
                Win32Helper.DestroyIcon(hIcon);
            }
        }

        var uwpAppsTask = Task.Run(() => GetUwpApps(uninstallableOnly));
        var win32AppsTask = Task.Run(GetWin32Apps);

        await Task.WhenAll(uwpAppsTask, win32AppsTask);

        var installedApps = uwpAppsTask.Result.Concat(win32AppsTask.Result).ToList();

        installedApps = [.. installedApps
            .DistinctBy(app => app.DisplayName)
            .OrderBy(app => app.DisplayName)];

        ErrorLogging.LogDebug(new Exception("Returning Installed Apps [GetInstalledApps]"));
        return installedApps;
    }

    private static async Task<List<SystemAppItem>> GetUwpApps(bool uninstallableOnly)
    {
        var installedApps = new List<SystemAppItem>();

        var command = uninstallableOnly
            ? """Get-AppxPackage -AllUsers | Where-Object { $_.NonRemovable -eq $false } | Select-Object Name,InstallLocation,PackageFullName,Version | Format-List"""
            : """Get-AppxPackage -AllUsers | Select-Object Name,InstallLocation,PackageFullName,Version | Format-List""";

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"{command}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            string? currentName = null;
            string? currentLocation = null;
            string? currentVersion = null;

            foreach (var line in output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("Name", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(currentName) && !string.IsNullOrEmpty(currentLocation))
                    {
                        var logoPath = await ExtractLogoPath(currentLocation, false, currentName).ConfigureAwait(false);
                        installedApps.Add(new SystemAppItem
                        {
                            DisplayName = currentName,
                            IconPath = logoPath,
                            IsWin32 = false,
                            InstallLocation = currentLocation,
                            Version = currentVersion ?? ""
                        });
                    }

                    currentName = line.Split([':'], 2)[1].Trim();
                    currentLocation = null;
                    currentVersion = null;
                }
                else if (line.StartsWith("InstallLocation", StringComparison.Ordinal))
                {
                    currentLocation = line.Split([':'], 2)[1].Trim();
                }
                else if (line.StartsWith("Version", StringComparison.Ordinal))
                {
                    currentVersion = line.Split([':'], 2)[1].Trim();
                }
                else if (!string.IsNullOrWhiteSpace(currentLocation) && line.StartsWith(" ", StringComparison.Ordinal))
                {
                    currentLocation += " " + line.Trim();
                }
            }

            if (!string.IsNullOrEmpty(currentName) && !string.IsNullOrEmpty(currentLocation))
            {
                var logoPath = await ExtractLogoPath(currentLocation, false, currentName).ConfigureAwait(false);
                installedApps.Add(new SystemAppItem
                {
                    DisplayName = currentName,
                    IconPath = logoPath,
                    IsWin32 = false,
                    InstallLocation = currentLocation,
                    Version = currentVersion ?? ""
                });
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            ErrorLogging.LogWritingFile(ex);
        }

        return installedApps;
    }

    public static async Task<List<SystemAppItem>> GetWin32Apps()
    {
        var win32Apps = new List<SystemAppItem>();
        var registryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        try
        {
            using var machineKey = Registry.LocalMachine.OpenSubKey(registryPath);
            using var userKey = Registry.CurrentUser.OpenSubKey(registryPath);

            var allSubKeys = (machineKey?.GetSubKeyNames() ?? Enumerable.Empty<string>())
                .Concat(userKey?.GetSubKeyNames() ?? Enumerable.Empty<string>())
                .Distinct();

            foreach (var subKeyName in allSubKeys)
            {
                using var subKey = machineKey?.OpenSubKey(subKeyName) ?? userKey?.OpenSubKey(subKeyName);

                if (subKey == null)
                {
                    continue;
                }

                var displayName = subKey.GetValue("DisplayName") as string;
                var installLocation = subKey.GetValue("InstallLocation") as string;

                if (!string.IsNullOrEmpty(installLocation))
                {
                    installLocation = installLocation.Replace("\"", "");
                    if (installLocation.Contains(".exe", StringComparison.OrdinalIgnoreCase))
                        installLocation = Path.GetDirectoryName(installLocation);
                }

                var uninstallString = subKey.GetValue("UninstallString") as string;
                if (!string.IsNullOrEmpty(uninstallString))
                {
                    uninstallString = uninstallString.Replace("\"", "");
                }

                var systemComponent = subKey.GetValue("SystemComponent") as int?;

                if (string.IsNullOrEmpty(displayName) || systemComponent == 1)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(installLocation) && !string.IsNullOrEmpty(uninstallString))
                {
                    try
                    {
                        installLocation = Path.GetDirectoryName(uninstallString);
                        if (!string.IsNullOrEmpty(installLocation) && installLocation.Contains(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            installLocation = Path.GetDirectoryName(installLocation);
                        }
                    }
                    catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                }

                if (displayName.Contains("edge", StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                var logoPath = await ExtractLogoPath(installLocation, true, displayName);

                var appItem = new SystemAppItem
                {
                    DisplayName = displayName,
                    IconPath = logoPath,
                    IsWin32 = true,
                    InstallLocation = installLocation ?? "",
                    UninstallString = uninstallString ?? "",
                    Version = subKey.GetValue("DisplayVersion")?.ToString() ?? ""
                };

                if (subKey.GetValue("EstimatedSize") is int sizeKb)
                {
                    appItem.SizeMB = sizeKb / 1024.0;
                }

                var dateStr = subKey.GetValue("InstallDate")?.ToString();
                if (!string.IsNullOrEmpty(dateStr) && dateStr.Length == 8)
                {
                    if (DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                    {
                        appItem.InstallDate = parsedDate;
                    }
                }

                win32Apps.Add(appItem);
            }
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            ErrorLogging.LogWritingFile(new Exception($"Failed to load Win32 apps: {ex.Message}"));
        }

        return [.. win32Apps
            .DistinctBy(app => app.DisplayName)
            .OrderBy(app => app.DisplayName)];
    }

    private static async Task<string> ExtractLogoPath(string? installLocation, bool isWin32 = false, string? appName = null)
    {
        var logoPath = Path.Combine(IconCacheDirectory, "defaulticon.png");

        if (isWin32)
        {
            try
            {
                if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                {
                    var nameForIcon = appName ?? Path.GetFileName(installLocation) ?? "Unknown";
                    var cachedIconPath = Path.Combine(IconCacheDirectory, GetSafeIconFileName(nameForIcon));

                    if (File.Exists(cachedIconPath) && new FileInfo(cachedIconPath).Length > 0)
                    {
                        return cachedIconPath;
                    }

                    if (!Directory.Exists(IconCacheDirectory))
                    {
                        Directory.CreateDirectory(IconCacheDirectory);
                    }

                    var iconIcoPath = Path.Combine(installLocation, "app.ico");
                    var iconPngPath = Path.Combine(installLocation, "icon.png");

                    if (File.Exists(iconIcoPath))
                    {
                        try
                        {
                            using var icon = new Icon(iconIcoPath);
                            await SaveIconAsPng(icon, cachedIconPath);
                            logoPath = cachedIconPath;
                        }
                        catch (Exception ex)
                        {
                            ErrorLogging.LogDebug(ex);
                            ErrorLogging.LogWritingFile(new Exception($"Failed to copy existing .ico file: {ex.Message}"));
                            logoPath = iconIcoPath;
                        }
                    }
                    else if (File.Exists(iconPngPath))
                    {
                        try
                        {
                            File.Copy(iconPngPath, cachedIconPath, true);
                            logoPath = cachedIconPath;
                        }
                        catch (Exception ex)
                        {
                            ErrorLogging.LogDebug(ex);
                            ErrorLogging.LogWritingFile(new Exception($"Failed to copy existing .png file: {ex.Message}"));
                            logoPath = iconPngPath;
                        }
                    }
                    else
                    {
                        var exeFile = Directory.GetFiles(installLocation, "*.exe").FirstOrDefault();
                        if (!string.IsNullOrEmpty(exeFile))
                        {
                            try
                            {
                                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exeFile);
                                if (icon != null)
                                {
                                    await SaveIconAsPng(icon, cachedIconPath);
                                    logoPath = cachedIconPath;
                                }
                            }
                            catch (Exception ex)
                            {
                                ErrorLogging.LogDebug(ex);
                                ErrorLogging.LogWritingFile(new Exception($"Failed to extract icon from executable: {ex.Message}"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                ErrorLogging.LogWritingFile(new Exception($"Failed to extract logo for Win32 app: {ex.Message}"));
            }
        }
        else
        {
            try
            {
                if (string.IsNullOrEmpty(installLocation)) return logoPath;

                var packageName = Path.GetFileName(installLocation).ToLower();
                if (packageName.Contains("sechealth"))
                {
                    logoPath = Path.Combine(installLocation, "Assets", "WindowsSecurityAppList.targetsize-48.png");
                }
                else if (packageName.Contains("edge"))
                {
                    logoPath = Path.Combine(installLocation, "SmallLogo.png");
                }
                else
                {
                    string[] possibleManifestPaths = {
                        Path.Combine(installLocation, "AppxManifest.xml"),
                        Path.Combine(installLocation, "appxmanifest.xml")
                    };

                    var manifestPath = possibleManifestPaths.FirstOrDefault(File.Exists);

                    if (manifestPath != null)
                    {
                        var doc = XDocument.Load(manifestPath);
                        XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";

                        var logoElement = doc.Descendants(ns + "Logo").FirstOrDefault();
                        if (logoElement != null)
                        {
                            var relativeLogoPath = logoElement.Value.Replace('/', '\\');
                            var baseLogoName = Path.GetFileNameWithoutExtension(relativeLogoPath);
                            var logoDirectory = Path.Combine(installLocation, Path.GetDirectoryName(relativeLogoPath) ?? "");

                            if (Directory.Exists(logoDirectory))
                            {
                                var exactLogoPath = Path.Combine(logoDirectory, relativeLogoPath);
                                if (File.Exists(exactLogoPath))
                                {
                                    logoPath = exactLogoPath;
                                }
                                else
                                {
                                    var logoFiles = Directory.GetFiles(logoDirectory, $"{baseLogoName}.Scale-*.png");
                                    var selectedLogoFile = logoFiles
                                        .OrderBy(f => Math.Abs(GetScaleFromFileName(f) - 200))
                                        .FirstOrDefault();

                                    if (!string.IsNullOrEmpty(selectedLogoFile) && File.Exists(selectedLogoFile))
                                    {
                                        logoPath = selectedLogoFile;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                ErrorLogging.LogWritingFile(new Exception($"Failed to extract logo path: {ex.Message}"));
            }
        }
        return logoPath;
    }

    private static async Task SaveIconAsPng(System.Drawing.Icon icon, string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new MemoryStream();

            using (var bitmap = icon.ToBitmap())
            {
                bitmap.Save(stream, ImageFormat.Png);
            }

            byte[] data = stream.ToArray();
            await File.WriteAllBytesAsync(filePath, data);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            ErrorLogging.LogWritingFile(new Exception($"Failed to save icon as PNG to {filePath}: {ex.Message}"));
        }
    }

    private static string GetSafeIconFileName(string name)
    {
        string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
        return Regex.Replace(name, invalidRegStr, "_") + ".png";
    }

    private static int GetScaleFromFileName(string fileName)
    {
        var match = Regex.Match(fileName, @"Scale-(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 100;
    }

    public static long GetDirectorySize(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;

        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                            .Sum(t => new FileInfo(t).Length);
        }
        catch
        {
            return 0;
        }
    }

    internal static async Task ExecuteBatchFileAsync()
    {
        try
        {
            ErrorLogging.LogDebug(new Exception("Executing Advanced Edge Removal Logic via AppManager"));

            await UninstallingPackages.RemoveAppxPackage("Edge", true);

            ErrorLogging.LogDebug(new Exception("Advanced Edge Removal completed successfully."));
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            ErrorLogging.LogWritingFile(new Exception($"Error executing advanced Edge removal: {ex.Message}"));
        }
    }

    public static async Task<bool> RemoveTempFiles()
    {
        try
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var browserPaths = new[]
            {
            Path.Combine(localApp, @"Google\Chrome\User Data\Default\Cache"),
            Path.Combine(localApp, @"Microsoft\Edge\User Data\Default\Cache"),
            Path.Combine(localApp, @"BraveSoftware\Brave-Browser\User Data\Default\Cache")
        };

            foreach (var path in browserPaths)
            {
                if (Directory.Exists(path))
                {
                    UnlockHandleHelper.UnlockDirectory(path);
                }
            }

            await CommandExecutor.RunCommand("taskkill /F /IM explorer.exe", isPowerShell: false).ConfigureAwait(false);

            var explorerDependentCommands = new[]
            {
            "rd /S /Q %localappdata%\\Temp",
            "rd /S /Q %localappdata%\\Microsoft\\Windows\\INetCache",
            "del /A /F /Q %localappdata%\\Microsoft\\Windows\\Explorer\\iconcache*",
            "del /A /F /Q %localappdata%\\Microsoft\\Windows\\Explorer\\thumbcache*",
            "rd /S /Q %windir%\\Prefetch"
        };

            foreach (var cmd in explorerDependentCommands)
            {
                await CommandExecutor.RunCommand(cmd, isPowerShell: false).ConfigureAwait(false);
            }

            await CommandExecutor.RunCommand("start explorer.exe", isPowerShell: false).ConfigureAwait(false);

            var tempCommands = new[]
            {
            "rd /S /Q %windir%\\Temp",
            "rd /S /Q %TEMP%",
            "rd /S /Q %windir%\\SoftwareDistribution\\Download",
            "rd /S /Q %windir%\\SoftwareDistribution\\DeliveryOptimization",
            "del /F /S /Q %windir%\\Logs\\CBS\\*",
            "del /F /Q %windir%\\MEMORY.DMP",
            "del /F /S /Q %windir%\\Minidump\\*.dmp",
            "rd /S /Q %programdata%\\Microsoft\\Windows\\WER\\ReportQueue",
            "rd /S /Q %localappdata%\\Microsoft\\Windows\\WER\\ReportArchive",
            "rd /S /Q %systemdrive%\\Windows.old",
            "rd /S /Q %systemdrive%\\MSOCache",
            "del /F /S /Q %systemdrive%\\*.tmp",
            "del /F /S /Q %systemdrive%\\*.log",
            "del /F /S /Q %userprofile%\\recent\\*.*",
            "Remove-Item -Path \"$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue",
            "Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\\Default\\Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue",
            "Remove-Item -Path \"$env:LOCALAPPDATA\\BraveSoftware\\Brave-Browser\\User Data\\Default\\Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue",
            "Clear-RecycleBin -Force -ErrorAction SilentlyContinue",
            "ipconfig /flushdns",
            "dism /Online /Cleanup-Image /StartComponentCleanup /Quiet"
        };

            foreach (var cmd in tempCommands)
            {
                bool usePS = cmd.Contains("Remove-Item") || cmd.Contains("Clear-RecycleBin");
                await CommandExecutor.RunCommand(cmd, isPowerShell: usePS).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorLogging.LogWritingFile(ex);
            return false;
        }
    }
}