using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using DnsClient;
using EvolveOS_Optimizer.Utilities.Controls;
using static EvolveOS_Optimizer.Core.Structs;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public partial class DNSCryptHelper
    {
        private const string DNSCryptRelease = "2.1.15";

        #region Paths & Constants
        private static readonly string UnzipDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "DNSCrypt"
            );

        private static readonly string InstallationDirectory =
            Path.Combine(
                UnzipDirectory,
                "win" + (Environment.Is64BitProcess ? "64" : "32")
            );

        private static readonly string BinaryPath =
            Path.Combine(
                InstallationDirectory,
                "dnscrypt-proxy.exe"
            );

        private static readonly string ConfigPath =
            Path.Combine(
                InstallationDirectory,
                "dnscrypt-proxy.toml"
            );

        private static readonly string ConfigExamplePath =
            Path.Combine(
                InstallationDirectory,
                "example-dnscrypt-proxy.toml"
            );
        #endregion

        #region Constructor
        static DNSCryptHelper()
        {

        }

        public static bool IsInstalled()
        {
            if (!Directory.Exists(InstallationDirectory))
            {

                return false;
            }

            if (!File.Exists(BinaryPath))
            {
                return false;
            }

            if (!File.Exists(ConfigPath))
            {
                return false;
            }

            return true;
        }

        public static bool IsRunning()
        {
            try
            {
                var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 53);
                var client = new LookupClient(new LookupClientOptions(endpoint)
                {
                    Retries = 0,
                    Timeout = TimeSpan.FromMilliseconds(200),
                });

                client.Query("github.com", QueryType.A);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static DNSServerEntry GetDNSServer()
        {
            return new DNSServerEntry
            {
                Name = "DNSCrypt",
                DnsEntries = new[]
                {
                    new DNSEntry("127.0.0.1"),
                    new DNSEntry("::1"),
                },
            };
        }
        #endregion

        #region Service & Installation Management
        public static async Task<bool> Install(ProgressBar progressBar, TextBlock statusLabel)
        {
            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = 5;
                progressBar.Value = 0;
                statusLabel.Text = "Initializing installation process...";
            });

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };

            var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            var bits = Environment.Is64BitProcess ? "64" : "32";
            byte[] buffer;

            try
            {
                progressBar.DispatcherQueue.TryEnqueue(() =>
                {
                    statusLabel.Text = $"Downloading dnscrypt-proxy {DNSCryptRelease} (x{bits}) from GitHub...";
                });

                buffer = await http.GetByteArrayAsync($"https://github.com/DNSCrypt/dnscrypt-proxy/releases/download/{DNSCryptRelease}/dnscrypt-proxy-win{bits}-{DNSCryptRelease}.zip");

                progressBar.DispatcherQueue.TryEnqueue(() =>
                {
                    progressBar.Value = 1;
                    statusLabel.Text = "Preparing installation directory...";
                });
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);

                progressBar.DispatcherQueue.TryEnqueue(() =>
                {
                    statusLabel.Text = $"[!] Error message: {ex.Message}";
                });
                return false;
            }

            await Task.Run(() =>
            {
                if (Directory.Exists(UnzipDirectory))
                {
                    Directory.Delete(UnzipDirectory, true);
                }
                Directory.CreateDirectory(UnzipDirectory);
            });

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 2;
                statusLabel.Text = "Extracting files...";
            });

            await Task.Run(() =>
            {
                var tempPath = Path.GetTempFileName() + ".zip";
                File.WriteAllBytes(tempPath, buffer);
                ZipFile.ExtractToDirectory(tempPath, UnzipDirectory);
                File.Delete(tempPath);
            });

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 3;
                statusLabel.Text = "Setting up configuration...";
            });

            await Task.Run(() =>
            {
                File.Copy(ConfigExamplePath, ConfigPath, true);
            });

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 4;
                statusLabel.Text = "Installing and starting DNSCrypt service...";
            });

            await Task.Run(() => { ExecuteProcessHidden(BinaryPath, "-service install"); });
            await Task.Run(() => { ExecuteProcessHidden(BinaryPath, "-service start"); });

            var counter = 1;
            while (!IsRunning())
            {
                progressBar.DispatcherQueue.TryEnqueue(() =>
                {
                    statusLabel.Text = $"Waiting for service to run ({counter++} s)...";
                });
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 5;
                statusLabel.Text = "Installation successful.";
            });

            return true;
        }

        public static bool Uninstall(ProgressBar progressBar, TextBlock statusLabel)
        {
            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = 1;
                progressBar.Value = 0;
                statusLabel.Text = "Deleting DNSCrypt files...";
            });

            try
            {
                Directory.Delete(UnzipDirectory, true);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);

                progressBar.DispatcherQueue.TryEnqueue(() =>
                {
                    statusLabel.Text = $"[!] Error message: {ex.Message}";
                });
                return false;
            }

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 1;
                statusLabel.Text = "Uninstallation successful.";
            });

            return true;
        }

        public static async Task StartService(ProgressBar progressBar, TextBlock statusLabel)
        {
            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = 3;
                progressBar.Value = 0;
                statusLabel.Text = "Installing DNSCrypt service...";
            });

            await Task.Run(() =>
            {
                ExecuteProcessHidden(BinaryPath, "-service install");
            });

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 1;
                statusLabel.Text = "Starting DNSCrypt service...";
            });

            await Task.Run(() =>
            {
                ExecuteProcessHidden(BinaryPath, "-service start");
            });

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 2;
            });

            var counter = 1;
            while (!IsRunning())
            {
                progressBar.DispatcherQueue.TryEnqueue(() =>
                {
                    statusLabel.Text = $"Starting DNSCrypt service ({counter++} s)...";
                });
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 3;
                statusLabel.Text = "Service start successful.";
            });
        }

        public static async Task StopService(ProgressBar progressBar, TextBlock statusLabel)
        {
            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = 3;
                progressBar.Value = 0;
                statusLabel.Text = "Stopping DNSCrypt service...";
            });

            await Task.Run(() =>
            {
                ExecuteProcessHidden(BinaryPath, "-service stop");
            });

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 1;
            });

            var counter = 1;
            while (IsRunning())
            {
                progressBar.DispatcherQueue.TryEnqueue(() =>
                {
                    statusLabel.Text = $"Stopping DNSCrypt service ({counter++} s)...";
                });
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 2;
                statusLabel.Text = "Uninstalling DNSCrypt service...";
            });

            await Task.Run(() =>
            {
                ExecuteProcessHidden(BinaryPath, "-service uninstall");
            });

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 3;
                statusLabel.Text = "Service stop successful.";
            });
        }

        public static async Task OpenConfig()
        {
            await Task.Run(() =>
            {
                ExecuteProcess("explorer", $"\"{ConfigPath}\"");
            });
        }

        public static async Task DebugProcess(ProgressBar progressBar, TextBlock statusLabel)
        {
            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = 1;
                progressBar.Value = 0;
                statusLabel.Text = "Debugging DNSCrypt process...";
            });

            await Task.Run(() =>
            {
                ExecuteProcess(BinaryPath);
            });

            progressBar.DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = 1;
                statusLabel.Text = "Debug process successful.";
            });
        }
        #endregion

        #region Configuration Management
        public static void SaveConfig(string config)
        {
            File.WriteAllText(ConfigPath, config);
        }

        public static string LoadConfig()
        {
            return File.ReadAllText(ConfigPath);
        }

        public static string GetCurrentSetting(string config, string name)
        {
            var match = new Regex($@"^(?:# )?{name} =\s*(?<value>.*?)\s*$", RegexOptions.Multiline).Match(config);
            if (!match.Success)
            {
                return string.Empty;
            }

            return match.Groups["value"].Value;
        }

        public static string SetSetting(string config, string name, string value)
        {
            var match = new Regex($@"^(?:# )?{name} =\s*(?<value>.*?)\s*$", RegexOptions.Multiline).Match(config);
            if (!match.Success)
            {
                if (!config.EndsWith(Environment.NewLine))
                {
                    config += Environment.NewLine;
                }

                return config + $"{name} = {value}{Environment.NewLine}";
            }

            var index = match.Groups[0].Index;
            var length = match.Groups[0].Length;

            return config.Substring(0, index) + $"{name} = {value}{Environment.NewLine}" + config.Substring(index + length);
        }
        #endregion

        #region Process Execution Helpers
        private static void ExecuteProcessHidden(string path, string arguments = "")
        {
            var psi = new ProcessStartInfo
            {
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path),
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "runas",
            };

            var process = new Process { StartInfo = psi };

            process.Start();
            process.WaitForExit();
        }

        private static void ExecuteProcess(string path, string arguments = "")
        {
            Process.Start(path, arguments).WaitForExit();
        }
        #endregion
    }
}