// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
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
                // 1. Check if we already have the perfect identity
                var package = Windows.ApplicationModel.Package.Current;
                return;
            }
            catch
            {
                // 2. We don't have identity. Time to bootstrap!
                try
                {
                    string? exePath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exePath))
                    {
                        exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    }

                    string exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
                    string msixPath = Path.Combine(exeDir, "EvolveOS_Lighting.msix");
                    var packageManager = new PackageManager();

                    var packages = packageManager.FindPackagesForUser(string.Empty, "EvolveOS.Optimizer").ToList();

                    // If it's NOT installed, install it now
                    if (packages.Count == 0)
                    {
                        InstallTrustedCertificate();

                        if (!File.Exists(msixPath))
                        {
                            byte[] msixBytes = ArchiveManager.GetResourceBytes("EvolveOS_Lighting.msix");
                            if (msixBytes.Length > 0)
                            {
                                ArchiveManager.ExtractRawResource(msixPath, msixBytes);
                            }
                            else
                            {
                                MessageBox(IntPtr.Zero, "Failed to extract .msix! Is the Build Action set to Embedded Resource?", "Extraction Error", 0x10);
                                return;
                            }
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

                        packages = packageManager.FindPackagesForUser(string.Empty, "EvolveOS.Optimizer").ToList();
                    }

                    // 3. RESTART WITH IDENTITY VIA EXPLORER
                    // (Because your app.manifest has requireAdministrator, Windows will automatically pop UAC here!)
                    if (packages.Count > 0)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true
                        });

                        Environment.Exit(0);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox(IntPtr.Zero, $"Critical Crash in Bootstrapper:\n\n{ex.Message}", "Fatal Error", 0x10);
                }
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