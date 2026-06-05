using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Controls
{
    internal static class ErrorLogging
    {
        [Conditional("DEBUG")]
        internal static void LogDebug(Exception ex, [CallerMemberName] string memberName = "") => Debug.WriteLine($"Debug: {ex.Message}\nStack Trace: {ex.StackTrace}\nMember: {memberName}\n");
        [Conditional("DEBUG")]
        internal static void LogDebug(string message, [CallerMemberName] string memberName = "") => Debug.WriteLine($"Debug: {message}\nMember: {memberName}\n");

        internal static void LogWritingFile(Exception ex, [CallerMemberName] string memberName = "") => Task.Run(() => LogToFile(ex, memberName)).Wait();

        internal static async Task LogInfo(string message, [CallerMemberName] string memberName = "")
        {
            // Outputs to the Visual Studio Debug console
            Debug.WriteLine($"[INFO] {memberName}: {message}");

            // try { await File.AppendAllTextAsync(PathLocator.Files.ErrorLog.Replace(".log", "_Info.log"), $"[{DateTime.Now}] {memberName}: {message}\n"); } catch { }

            await Task.CompletedTask;
        }

        private static async Task EnsureAssociation()
        {
            try
            {
                string assocLogFile = await CommandExecutor.GetCommandOutput("/c assoc .log", false);

                if (assocLogFile.IndexOf("=", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    string assocTxtFile = await CommandExecutor.GetCommandOutput("/c assoc .txt", false);

                    if (assocTxtFile.IndexOf("=", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        await CommandExecutor.RunCommand($"/c assoc .log={assocTxtFile.Split('=')[1].Trim()}");
                    }
                }
            }
            catch (Exception fileEx) { LogDebug(fileEx); }
        }

        private static async Task LogToFile(Exception ex, string? memberName)
        {
            try
            {
                using (FileStream stream = new FileStream(PathLocator.Files.ErrorLog, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    string headerLine = "---------------------------------------------------------";
                    Exception? currentEx = ex;
                    byte exLevel = 1;

                    await writer.WriteLineAsync($"EvolveOS Optimizer has crashed!\n{headerLine}\nIf you wish to report this, please open an issue here:\nhttps://github.com/EvolveOS/EvolveOS_Optimizer/issues\n{headerLine}\n");
                    await writer.WriteLineAsync($"{headerLine}\n[{DateTime.Now}]\nOS: {(string.IsNullOrEmpty(HardwareData.OS?.Name) ? "Unknown (Loading error)" : HardwareData.OS.Name)}\nRelease: {SettingsEngine.currentRelease}\n{headerLine}\n");

                    while (currentEx != null)
                    {
                        await writer.WriteLineAsync($"Exception Level: {exLevel}");

                        if (exLevel == 1)
                        {
                            await writer.WriteLineAsync($"Member: {memberName ?? "Unknown"}");
                        }

                        await writer.WriteLineAsync($"Type: {currentEx.GetType().FullName}");
                        await writer.WriteLineAsync($"Error: {currentEx.Message}");

                        StackTrace stackTrace = new StackTrace(currentEx, true);

                        if (stackTrace.FrameCount > 0)
                        {
                            StackFrame? frame = stackTrace.GetFrame(0);

                            if (frame?.GetMethod() is MethodBase method)
                            {
                                await writer.WriteLineAsync($"Method: {method.DeclaringType?.FullName}.{method.Name}");

                                ParameterInfo[] parameters = method.GetParameters();
                                if (parameters.Length > 0)
                                {
                                    await writer.WriteLineAsync($"Parameters:");
                                    foreach (var param in parameters)
                                    {
                                        await writer.WriteLineAsync($"{param.Name ?? "unnamed"}: {param.ParameterType}");
                                    }
                                }
                            }
                        }

                        await writer.WriteLineAsync($"Stack Trace:\n{currentEx.StackTrace}");
                        await writer.WriteLineAsync($"\n{headerLine}\n");

                        currentEx = currentEx.InnerException;
                        exLevel++;
                    }

                    await writer.FlushAsync();
                }

                await EnsureAssociation();

                if (File.Exists(PathLocator.Files.ErrorLog))
                {
                    try
                    {
                        await Task.Delay(50);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = PathLocator.Files.ErrorLog,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception processEx) { LogDebug(processEx); }
                }
            }
            catch (Exception fileEx) { LogDebug(fileEx); }
        }
    }
}