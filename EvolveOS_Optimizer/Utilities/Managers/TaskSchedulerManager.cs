using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Storage;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    internal class TaskSchedulerManager : TaskStorage
    {
        internal static bool IsTaskEnabled(params string[] tasklist)
        {
            if (tasklist == null || tasklist.Length == 0) return false;

            bool isEnabledFound = false;

            Parallel.ForEach(tasklist, () => new Microsoft.Win32.TaskScheduler.TaskService(),
            (taskName, loopState, taskScheduler) =>
            {
                try
                {
                    if (isEnabledFound)
                    {
                        loopState.Stop();
                        return taskScheduler;
                    }

                    using (Microsoft.Win32.TaskScheduler.Task scheduledTask = taskScheduler.GetTask(taskName))
                    {
                        if (scheduledTask != null && scheduledTask.Enabled)
                        {
                            isEnabledFound = true;
                            loopState.Stop();
                        }
                    }
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }

                return taskScheduler;
            },
            taskScheduler =>
            {
                try { taskScheduler.Dispose(); }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            });

            return isEnabledFound;
        }

        internal static Task SetTaskState(bool state, CancellationToken token = default, params string[] tasklist)
        {
            return Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;

                string[] existingTasks = GetExistingTasks(tasklist);

                if (existingTasks.Length != 0)
                {
                    using (Microsoft.Win32.TaskScheduler.TaskService taskService = new Microsoft.Win32.TaskScheduler.TaskService())
                    {
                        foreach (string taskname in existingTasks)
                        {
                            if (token.IsCancellationRequested) break;

                            try
                            {
                                using (Microsoft.Win32.TaskScheduler.Task task = taskService.GetTask(taskname))
                                {
                                    if (task != null && task.Enabled != state)
                                    {
                                        task.Definition.Settings.Enabled = state;
                                        task.RegisterChanges();
                                    }
                                }
                            }
                            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                        }
                    }
                }
            }, token);
        }

        internal static void SetTaskStateOwner(bool state, CancellationToken token = default, params string[] tasklist)
        {
            _ = Task.Run(async () =>
            {
                if (token.IsCancellationRequested) return;

                string[] existingTasks = GetExistingTasks(tasklist);

                if (existingTasks.Length != 0)
                {
                    try
                    {
                        string commands = string.Join(" & ", existingTasks.Select(task => $"schtasks /change {(state ? "/enable" : "/disable")} /tn \"{task}\""));
                        await CommandExecutor.RunCommandAsTrustedInstaller("/c " + CommandExecutor.CleanCommand(commands));
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                }
            }, token);
        }

        internal static async Task RemoveTasksAsync(CancellationToken token = default, params string[] tasklist)
        {
            await Task.Run(async () =>
            {
                if (token.IsCancellationRequested) return;

                string[] existingTasks = GetExistingTasks(tasklist);

                if (existingTasks.Length != 0)
                {
                    try
                    {
                        string commands = string.Join(" & ", existingTasks.Select(task => $"schtasks /delete /tn \"{task}\" /f"));
                        await CommandExecutor.RunCommand("/c " + CommandExecutor.CleanCommand(commands));
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                }
            }, token);
        }

        internal static string GetTaskFullPath(string partialName)
        {
            if (string.IsNullOrEmpty(partialName)) return string.Empty;

            try
            {
                string[] files = Directory.GetFiles(PathLocator.Folders.Tasks, "*", SearchOption.AllDirectories);

                List<string> matches = files.Where(path => Path.GetFileName(path).StartsWith(partialName, StringComparison.OrdinalIgnoreCase)).ToList();

                if (matches.Count != 0)
                {
                    string relativePath = matches[0].Substring(PathLocator.Folders.Tasks.Length).Replace(Path.DirectorySeparatorChar, '\\');
                    relativePath = Regex.Replace(relativePath, @"^\\+", "");
                    return $@"\{relativePath}";
                }
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }

            return $@"\{partialName}*";
        }

        internal static string[] GetAllTasksInPaths(params string[] basePaths)
        {
            List<string> taskList = new List<string>();
            if (basePaths == null) return taskList.ToArray();

            foreach (var basePath in basePaths)
            {
                try
                {
                    string fullBasePath = Path.Combine(PathLocator.Folders.Tasks, basePath.TrimStart('\\'));

                    if (Directory.Exists(fullBasePath))
                    {
                        string[] files = Directory.GetFiles(fullBasePath, "*", SearchOption.AllDirectories);

                        foreach (string file in files)
                        {
                            string relativePath = file.Substring(PathLocator.Folders.Tasks.Length).Replace(Path.DirectorySeparatorChar, '\\');
                            relativePath = Regex.Replace(relativePath, @"^\\+", "");
                            taskList.Add(@"\" + relativePath);
                        }
                    }
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            }

            return taskList.ToArray();
        }

        private static string[] GetExistingTasks(params string[] tasklist)
        {
            if (tasklist == null) return Array.Empty<string>();

            List<string> foundExisting = new List<string>(tasklist.Length);

            foreach (string path in tasklist)
            {
                try
                {
                    string fullPath = Path.Combine(PathLocator.Folders.Tasks, path.TrimStart('\\', '/').Replace('/', '\\'));
                    if (File.Exists(fullPath))
                    {
                        foundExisting.Add(path);
                    }
                }
                catch { }
            }

            return foundExisting.ToArray();
        }
    }
}