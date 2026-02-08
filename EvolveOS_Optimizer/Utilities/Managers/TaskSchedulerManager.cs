using System.IO;
using Microsoft.Win32.TaskScheduler;
using EvolveOS_Optimizer.Utilities.Helpers;
using SchedulerTask = Microsoft.Win32.TaskScheduler.Task;
using SystemTask = System.Threading.Tasks.Task;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    internal class TaskSchedulerManager
    {
        internal static bool IsTaskEnabled(params string[] tasklist)
        {
            if (tasklist == null || tasklist.Length == 0) return false;

            bool isEnabledFound = false;
            object syncLock = new object();

            Parallel.ForEach(tasklist, () => new TaskService(),
            (taskName, loopState, taskService) =>
            {
                try
                {
                    if (isEnabledFound)
                    {
                        loopState.Stop();
                        return taskService;
                    }

                    using (SchedulerTask scheduledTask = taskService.GetTask(taskName))
                    {
                        if (scheduledTask != null && scheduledTask.Enabled)
                        {
                            lock (syncLock)
                            {
                                isEnabledFound = true;
                            }
                            loopState.Stop();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TaskScheduler] Error checking {taskName}: {ex.Message}");
                }

                return taskService;
            },
            taskService =>
            {
                taskService.Dispose();
            });

            return isEnabledFound;
        }

        internal static async SystemTask SetTaskState(bool state, params string[] tasklist)
        {
            await SystemTask.Run(() =>
            {
                string[] existingTasks = GetExistingTasks(tasklist);
                if (existingTasks.Length == 0) return;

                try
                {
                    using TaskService ts = new TaskService();
                    foreach (string taskName in existingTasks)
                    {
                        using (SchedulerTask task = ts.GetTask(taskName))
                        {
                            if (task != null && task.Enabled != state)
                            {
                                task.Definition.Settings.Enabled = state;
                                task.RegisterChanges();
                                Debug.WriteLine($"[TaskScheduler] Task '{taskName}' set to: {state}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TaskScheduler] Error setting task state: {ex.Message}");
                }
            });
        }

        internal static async SystemTask SetTaskStateOwner(bool state, params string[] tasklist)
        {
            if (tasklist == null || tasklist.Length == 0)
            {
                return;
            }

            string[] existingTasks = GetExistingTasks(tasklist);

            if (existingTasks.Length != 0)
            {
                string action = state ? "/enable" : "/disable";
                string commands = string.Join(" & ", existingTasks.Select(task => $"schtasks /change {action} /tn \"{task}\""));

                await CommandExecutor.RunCommandAsTrustedInstaller("/c " + commands, true);
            }
        }

        internal static async SystemTask RemoveTasks(params string[] tasklist)
        {
            await SystemTask.Run(() =>
            {
                string[] existingTasks = GetExistingTasks(tasklist);
                if (existingTasks.Length == 0) return;

                try
                {
                    using TaskService ts = new TaskService();
                    foreach (string taskName in existingTasks)
                    {
                        ts.RootFolder.DeleteTask(taskName, false);
                        Debug.WriteLine($"[TaskScheduler] Task '{taskName}' deleted.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TaskScheduler] Error removing tasks: {ex.Message}");
                }
            });
        }

        internal static string GetTaskFullPath(string partialName)
        {
            try
            {
                string taskRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "Tasks");
                if (!Directory.Exists(taskRoot)) return partialName;

                string[] files = Directory.GetFiles(taskRoot, "*", SearchOption.AllDirectories);
                var match = files.FirstOrDefault(path => string.Equals(Path.GetFileName(path), partialName, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    string relativePath = match.Substring(taskRoot.Length).Replace(Path.DirectorySeparatorChar, '\\');
                    return relativePath.StartsWith("\\") ? relativePath : "\\" + relativePath;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[TaskScheduler] FullPath Error: {ex.Message}"); }

            return partialName;
        }

        private static string[] GetExistingTasks(params string[] tasklist)
        {
            if (tasklist == null) return Array.Empty<string>();

            List<string> foundExisting = new List<string>(tasklist.Length);
            string taskRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "Tasks");

            foreach (string path in tasklist)
            {
                string localPath = path.TrimStart('\\', '/').Replace('/', '\\');
                if (File.Exists(Path.Combine(taskRoot, localPath)))
                {
                    foundExisting.Add(path);
                }
            }

            return foundExisting.ToArray();
        }
    }
}