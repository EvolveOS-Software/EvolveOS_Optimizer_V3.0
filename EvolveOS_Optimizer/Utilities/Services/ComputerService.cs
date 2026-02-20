using System.ComponentModel;
using System.Runtime.InteropServices;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model.Log;
using EvolveOS_Optimizer.Core.Model.MemoryModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Maintenance;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class ComputerService : IComputerService
    {
        #region Fields

        private Memory _memory = new Memory(new Structs.Windows.MemoryStatusEx());
        public Core.Model.OperatingSystem OperatingSystem => ClearingMemory.OperatingSystem;

        #endregion

        #region Properties

        public Memory Memory
        {
            get
            {
                try
                {
                    var memoryStatusEx = new Structs.Windows.MemoryStatusEx();

                    if (!Win32Helper.GlobalMemoryStatusEx(memoryStatusEx))
                    {
                        ErrorLogging.LogDebug(new Win32Exception(Marshal.GetLastWin32Error()));
                    }

                    _memory = new Memory(memoryStatusEx);
                }
                catch (Exception e)
                {
                    ErrorLogging.LogDebug(e);
                }

                return _memory;
            }
        }

        #endregion

        #region Events

        public event Action<byte, string>? OnOptimizeProgressUpdate;

        #endregion

        #region Methods (Memory)

        public async Task Optimize(Enums.Memory.Optimization.Reason reason, Enums.Memory.Areas areas)
        {
            if (areas == Enums.Memory.Areas.None)
            {
                return;
            }

            if (!ClearingMemory.SetIncreasePrivilege(Win32Helper.Privilege.SeProfSingleProcessName))
            {
                throw new Exception(string.Format($"this operation requires administrator privileges ({0})", Win32Helper.Privilege.SeProfSingleProcessName));
            }

            var errorRuntime = new TimeSpan();
            var infoRuntime = new TimeSpan();
            var optimizationReason = reason.ToString();
            var stopwatch = new Stopwatch();
            var value = (byte)0;

            var error = new LogOptimizationData { Reason = optimizationReason };
            var info = new LogOptimizationData { Reason = optimizationReason };

            async Task RunOptimizationStepAsync(string name, Func<Task> action)
            {
                try
                {
                    if (OnOptimizeProgressUpdate != null)
                    {
                        value++;
                        OnOptimizeProgressUpdate(value, name);
                    }

                    stopwatch.Restart();
                    await action();
                    stopwatch.Stop();

                    info.MemoryAreas.Add(new LogOptimizationDataMemoryArea
                    {
                        Name = name,
                        Duration = $"{stopwatch.Elapsed.TotalSeconds:0.0} seconds"
                    });
                    infoRuntime = infoRuntime.Add(stopwatch.Elapsed);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Debug.WriteLine($"Optimization Error in {name}: {ex.Message}");

                    error.MemoryAreas.Add(new LogOptimizationDataMemoryArea
                    {
                        Name = name,
                        Duration = $"{stopwatch.Elapsed.TotalSeconds:0.0} seconds"
                    });
                    errorRuntime = errorRuntime.Add(stopwatch.Elapsed);
                }
            }

            async Task RunSyncStepAsync(string name, Action action)
            {
                await RunOptimizationStepAsync(name, () => { action(); return Task.CompletedTask; });
            }

            if ((areas & Enums.Memory.Areas.WorkingSet) != 0)
            {
                await RunSyncStepAsync(ResourceString.GetString("optimizations_step_working_set"), () => ClearingMemory.EmptyWorkingSetFunction());
            }

            if ((areas & Enums.Memory.Areas.SystemFileCache) != 0)
            {
                await RunSyncStepAsync(ResourceString.GetString("optimizations_step_system_file_cache"), () => ClearingMemory.ClearFileSystemCache(false));
            }

            if ((areas & Enums.Memory.Areas.ModifiedPageList) != 0)
            {
                await RunSyncStepAsync(ResourceString.GetString("optimizations_step_modified_page_list"), () => ClearingMemory.OptimizeModifiedPageList());
            }

            if ((areas & (Enums.Memory.Areas.StandbyList | Enums.Memory.Areas.StandbyListLowPriority)) != 0)
            {
                bool lowPriority = (areas & Enums.Memory.Areas.StandbyListLowPriority) != 0;
                string label = lowPriority ? "optimizations_step_standby_list_lp" : "optimizations_step_standby_list";

                await RunSyncStepAsync(ResourceString.GetString(label), () => ClearingMemory.ClearFileSystemCache(true, lowPriority));
            }

            if ((areas & Enums.Memory.Areas.CombinedPageList) != 0)
            {
                await RunSyncStepAsync(ResourceString.GetString("optimizations_step_combined_page_list"), () => ClearingMemory.OptimizeCombinedPageList());
            }

            if ((areas & Enums.Memory.Areas.RegistryCache) != 0)
            {
                await RunSyncStepAsync(ResourceString.GetString("optimizations_step_registry_cache"), () => ClearingMemory.OptimizeRegistryCache());
            }

            if ((areas & Enums.Memory.Areas.ModifiedFileCache) != 0)
            {
                await RunSyncStepAsync(ResourceString.GetString("optimizations_step_modified_file_cache"), () => ClearingMemory.OptimizeModifiedFileCache());
            }

            if ((areas & Enums.Memory.Areas.DiskCleanup) != 0)
            {
                await RunOptimizationStepAsync(ResourceString.GetString("optimizations_step_disk_cleanup"), async () => await ClearingMemory.StartMemoryCleanup(clearRamCache: true, optimizeWorkingSet: false));
                await RunOptimizationStepAsync(ResourceString.GetString("optimizations_step_update_cache"), async () => await ClearingMemory.CleanSoftwareDistribution());
            }

            if ((areas & Enums.Memory.Areas.FlushDns) != 0)
            {
                await RunSyncStepAsync(ResourceString.GetString("optimizations_step_flush_dns"), () => ClearingMemory.FlushDnsCache());
            }

            if ((areas & Enums.Memory.Areas.WindowsOld) != 0)
            {
                await RunOptimizationStepAsync(ResourceString.GetString("optimizations_step_windows_old"), async () => await ClearingMemory.CleanWindowsOld());
            }

            try
            {
                if (OnOptimizeProgressUpdate != null)
                {
                    value++;
                    OnOptimizeProgressUpdate(value, ResourceString.GetString("optimizations_step_modified_garbage_collector"));
                }

                App.ReleaseMemory();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GC Error: {ex.Message}");
            }

            try
            {
                if (info.MemoryAreas.Any())
                {
                    info.Duration = $"{infoRuntime.TotalSeconds:0.0} seconds";
                }

                if (error.MemoryAreas.Any())
                {
                    error.Duration = $"{errorRuntime.TotalSeconds:0.0} seconds";
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        public void RefreshMemory()
        {
            try
            {
                var memoryStatusEx = new EvolveOS_Optimizer.Core.Structs.Windows.MemoryStatusEx();
                if (Win32Helper.GlobalMemoryStatusEx(memoryStatusEx))
                {
                    _memory = new Core.Model.MemoryModel.Memory(memoryStatusEx);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Memory Refresh Error: {ex.Message}");
            }
        }

        #endregion
    }
}