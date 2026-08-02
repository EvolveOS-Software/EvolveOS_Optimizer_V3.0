// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Windows.Management.Deployment;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class IdentityHelper
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public static async Task EnsureAppIdentityAsync()
        {
            try
            {
                var package = Windows.ApplicationModel.Package.Current;
                return;
            }
            catch
            {
                try
                {
                    string? exePath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exePath))
                    {
                        exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    }

                    string exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

                    string assetsDir = Path.Combine(exeDir, "Assets");
                    Directory.CreateDirectory(assetsDir);

                    ExtractResourceToDisk(Path.Combine(exeDir, "EvolveOS_Package.msix"), "EvolveOS_Package.msix");
                    ExtractResourceToDisk(Path.Combine(exeDir, "EvolveOS_MenuProxy.dll"), "EvolveOS_MenuProxy.dll");
                    ExtractResourceToDisk(Path.Combine(exeDir, "AppxManifest.xml"), "AppxManifest.xml");
                    ExtractResourceToDisk(Path.Combine(assetsDir, "EvolveOS_Optimizer-Logo.png"), "EvolveOS_Optimizer-Logo.png");

                    ExtractResourceToDisk(Path.Combine(exeDir, "EvolveOS_LightingProxy.dll"), "EvolveOS_LightingProxy.dll");
                    string zipPath = Path.Combine(exeDir, "OpenRGB_Server.zip");
                    string openRgbFolder = Path.Combine(exeDir, "OpenRGB_Server");

                    ExtractResourceToDisk(zipPath, "OpenRGB_Server.zip");

                    if (!Directory.Exists(openRgbFolder) && File.Exists(zipPath))
                    {
                        try
                        {
                            ZipFile.ExtractToDirectory(zipPath, openRgbFolder);
                            File.Delete(zipPath);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[IdentityHelper] Failed to unzip OpenRGB: {ex.Message}");
                        }
                    }

                    var packageManager = new PackageManager();
                    var packages = packageManager.FindPackagesForUser(string.Empty).Where(p => p.Id.Name == "EvolveOS.Optimizer").ToList();

                    if (packages.Count == 0)
                    {
                        InstallTrustedCertificate();

                        string msixPath = Path.Combine(exeDir, "EvolveOS_Package.msix");
                        if (!File.Exists(msixPath))
                        {
                            MessageBox(IntPtr.Zero, "Failed to locate EvolveOS_Package.msix!", "Extraction Error", 0x10);
                            return;
                        }

                        var options = new AddPackageOptions
                        {
                            ExternalLocationUri = new Uri(exeDir),
                            AllowUnsigned = true
                        };

                        var deploymentResult = await packageManager.AddPackageByUriAsync(new Uri(msixPath), options);

                        if (!deploymentResult.IsRegistered)
                        {
                            MessageBox(IntPtr.Zero, $"MSIX Install Failed!\n\nReason: {deploymentResult.ErrorText}\n\nCode: {deploymentResult.ExtendedErrorCode}", "Registration Error", 0x10);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox(IntPtr.Zero, $"Critical Crash in Bootstrapper:\n\n{ex.Message}", "Fatal Error", 0x10);
                }
            }
        }

        private static void ExtractResourceToDisk(string targetPath, string resourceName)
        {
            try
            {
                byte[] bytes = ArchiveManager.GetResourceBytes(resourceName);
                if (bytes.Length > 0)
                {
                    File.WriteAllBytes(targetPath, bytes);
                }
            }
            catch (IOException)
            {
                Debug.WriteLine($"[IdentityHelper] Skipping extraction of {resourceName}, file is currently in use by Explorer.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IdentityHelper] Failed to extract {resourceName}: {ex.Message}");
            }
        }

        private static void InstallTrustedCertificate()
        {
            try
            {
                byte[] certBytes = ArchiveManager.GetResourceBytes("EvolveOS_DevCert.cer");

                if (certBytes.Length > 0)
                {
                    using var cert = X509CertificateLoader.LoadCertificate(certBytes);
                    using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);

                    store.Open(OpenFlags.ReadWrite);
                    if (!store.Certificates.Contains(cert))
                    {
                        store.Add(cert);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox(IntPtr.Zero, $"Cert Install Failed.\n\n{ex.Message}", "Cert Error", 0x10);
            }
        }
    }
}