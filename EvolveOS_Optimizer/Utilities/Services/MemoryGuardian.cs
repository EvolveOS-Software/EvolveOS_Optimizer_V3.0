// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class MemoryGuardian
    {
        #region Fields & Properties
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer? _checkTimer;
        private readonly Action<ulong, ulong>? _onCleanupPerformed;
        private ulong _currentThresholdBytes;
        #endregion

        #region Native Interop
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);
        #endregion

        #region Constructor
        public MemoryGuardian(Action<ulong, ulong>? onCleanupPerformed = null)
        {
            _onCleanupPerformed = onCleanupPerformed;
            _currentThresholdBytes = 200 * 1024 * 1024;

            var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (queue == null) return;

            _checkTimer = queue.CreateTimer();
            _checkTimer.Interval = TimeSpan.FromSeconds(5);
            _checkTimer.Tick += (s, e) => MonitorAndCleanup();
            _checkTimer.Start();
        }
        #endregion

        #region Public API
        public void SetThreshold(int megabytes)
        {
            _currentThresholdBytes = (ulong)megabytes * 1024 * 1024;
            Debug.WriteLine($"[MemoryGuardian] Threshold adjusted to {megabytes}MB");
        }
        #endregion

        #region Core Logic
        private ulong GetAccurateMemoryUsage(Process process)
        {
            try
            {
                using var counter = new PerformanceCounter("Process", "Working Set - Private", process.ProcessName, true);
                return (ulong)counter.RawValue;
            }
            catch
            {
                return (ulong)process.PrivateMemorySize64;
            }
        }

        public void MonitorAndCleanup()
        {
            using var currentProcess = Process.GetCurrentProcess();

            ulong privateUsage = GetAccurateMemoryUsage(currentProcess);

            if (privateUsage > _currentThresholdBytes)
            {
                ulong physicalBefore = (ulong)currentProcess.WorkingSet64;

                Debug.WriteLine($"[MemoryGuardian] Task Manager Threshold exceeded: {privateUsage / 1024 / 1024}MB. Initiating Deep Cleanup...");

                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect();

                EmptyWorkingSet(currentProcess.Handle);

                currentProcess.Refresh();
                ulong physicalAfter = (ulong)currentProcess.WorkingSet64;

                Debug.WriteLine($"[MemoryGuardian] Cleanup complete. Physical RAM dropped to: {physicalAfter / 1024 / 1024}MB");

                _onCleanupPerformed?.Invoke(physicalBefore, physicalAfter);
            }
        }
        #endregion
    }
}