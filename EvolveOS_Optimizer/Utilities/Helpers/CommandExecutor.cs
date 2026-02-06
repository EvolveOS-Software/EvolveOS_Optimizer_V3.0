using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    internal static class CommandExecutor
    {
        internal static int PID = 0;

        internal static async Task<string> GetCommandOutput(string command, bool isPowerShell = true)
        {
            return await Task.Run(async () =>
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = isPowerShell ? PathLocator.Executable.PowerShell : PathLocator.Executable.CommandShell,
                    Arguments = isPowerShell ? $"-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"{command}\"" : command,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System) ?? ""
                };

                using Process process = new Process { StartInfo = startInfo };
                process.Start();

                string output = (await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false)) ?? string.Empty;
                string error = (await process.StandardError.ReadToEndAsync().ConfigureAwait(false)) ?? string.Empty;

                await process.WaitForExitAsync().ConfigureAwait(false);

                if (process.ExitCode == 0)
                {
                    return string.Join(Environment.NewLine, output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
                }
                else
                {
                    Debug.WriteLine($"{process.ExitCode}: {error}");
                    return string.Empty;
                }
            });
        }

        internal static async Task RunCommandAsTrustedInstaller(string command, bool isPowerShell = false)
        {
            try
            {
                if (PID == 0)
                {
                    var tiProcess = Process.GetProcessesByName("TrustedInstaller").FirstOrDefault();
                    if (tiProcess != null)
                    {
                        PID = tiProcess.Id;
                    }
                    else
                    {
                        return;
                    }
                }

                string formattedCommand = isPowerShell
                    ? $"{PathLocator.Executable.PowerShell} -NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "`\"")}\""
                    : $"{PathLocator.Executable.CommandShell} /c {command}";

                int childPid = TrustedInstaller.CreateProcessAsTrustedInstaller(PID, formattedCommand);

                if (childPid > 0)
                {
                    await WaitForProcessExitAsync(childPid);
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        private static async Task WaitForProcessExitAsync(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);

                var tcs = new TaskCompletionSource<bool>();
                process.EnableRaisingEvents = true;

                process.Exited += (object? sender, EventArgs e) => tcs.TrySetResult(true);

                if (process.HasExited)
                {
                    return;
                }

                await tcs.Task;
            }
            catch (ArgumentException)
            {
                // Process already exited
            }
        }

        internal static async Task RunCommand(string command, bool isPowerShell = false)
        {
            await Task.Run(() =>
            {
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = isPowerShell ? PathLocator.Executable.PowerShell : PathLocator.Executable.CommandShell,
                    Arguments = isPowerShell ? $"-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"{command}\"" : command,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                try
                {
                    using (Process? process = Process.Start(startInfo))
                    {
                        // No wait needed for fire-and-forget commands
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
            }).ConfigureAwait(false);
        }

        internal static async void RunCommandShow(string fileName, string arguments = "", bool isElevationRequired = false)
        {
            await Task.Run(() =>
            {
                if (isElevationRequired)
                {
                    TrustedInstaller.CreateProcessAsTrustedInstaller(PID, $"{fileName} {arguments}", true);
                }
                else
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo()
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WindowStyle = ProcessWindowStyle.Normal,
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = false
                    };

                    using Process process = new Process() { StartInfo = startInfo };
                    try { process.Start(); }
                    catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                }
            }).ConfigureAwait(false);
        }

        internal static async Task InvokeRunCommand(string command, bool isPowerShell = false)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = isPowerShell ? PathLocator.Executable.PowerShell : PathLocator.Executable.CommandShell,
                Arguments = isPowerShell ? $"-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"{command}\"" : command,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
        }

        internal static void ExecuteCommand(string fileName, string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                };

                using (Process? p = Process.Start(psi))
                {
                    p?.WaitForExit(10000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Command {arguments} failed: {ex.Message}");
            }
        }

        internal static Task WaitForExitAsync(this Process process)
        {
            if (process.HasExited)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<object?>();

            void Handler(object? s, EventArgs e) => tcs.TrySetResult(null);

            process.EnableRaisingEvents = true;
            process.Exited += Handler;

            tcs.Task.ContinueWith(_ => process.Exited -= Handler, TaskScheduler.Default);

            if (process.HasExited)
            {
                tcs.TrySetResult(null);
            }

            return tcs.Task;
        }

        internal static string CleanCommand(string? rawCommand)
        {
            if (string.IsNullOrWhiteSpace(rawCommand))
            {
                return string.Empty;
            }

            List<string> lines = rawCommand
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .Select(line => Regex.Replace(line, @"\s+", " "))
                .ToList();

            if (lines.Count == 0)
            {
                return string.Empty;
            }

            if (lines.Count == 1)
            {
                return lines[0];
            }

            string separator = (rawCommand?.Contains("&&") == true) ? " && " : (rawCommand?.Contains("&") == true) ? " & " : " && ";

            return string.Join(separator, lines);
        }
    }
}