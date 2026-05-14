// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class MemoryGuardian : IDisposable
    {
        #region Fields & Properties
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer? _checkTimer;
        private readonly Action<ulong, ulong>? _onCleanupPerformed;
        private PerformanceCounter? _ramCounter;

        private ulong _currentThresholdBytes;
        private ulong _emergencyThresholdBytes; // Active UI threshold

        private bool _isBackgroundMode = false; // Tracks if the app is minimized
        private int _highMemorySeconds = 0;

        private bool _isDisposed;

        private const int RequiredSustainedSeconds = 15; // Must stay high for 15s to trigger GC
        private const int TimerIntervalSeconds = 5;
        #endregion

        #region Native Interop
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);
        #endregion

        #region Constructor
        public MemoryGuardian(Action<ulong, ulong>? onCleanupPerformed = null)
        {
            _onCleanupPerformed = onCleanupPerformed;

            _currentThresholdBytes = 300 * 1024 * 1024; // 300MB Deep Clean (Background)
            _emergencyThresholdBytes = 600 * 1024 * 1024; // 600MB Gentle Clean (Active UI)

            InitializeCounter();

            var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (queue != null)
            {
                _checkTimer = queue.CreateTimer();
                _checkTimer.Interval = TimeSpan.FromSeconds(TimerIntervalSeconds);

                _checkTimer.Tick += (s, e) => CheckMemoryState();
                _checkTimer.Start();
            }
        }

        private void InitializeCounter()
        {
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                _ramCounter = new PerformanceCounter("Process", "Working Set - Private", currentProcess.ProcessName, true);
            }
            catch
            {
                Debug.WriteLine("[MemoryGuardian] PerformanceCounter failed. Falling back to PrivateMemorySize64.");
            }
        }
        #endregion

        #region Public API
        public void SetThreshold(int megabytes)
        {
            _currentThresholdBytes = (ulong)megabytes * 1024 * 1024;
            Debug.WriteLine($"[MemoryGuardian] Background Threshold adjusted to {megabytes}MB");
        }
        #endregion

        #region Control API (The "Silence" Fix)
        public void Pause()
        {
            LocalMachineSettingsEngine.IsGuardianPaused = true;

            _checkTimer?.Stop();
            Debug.WriteLine("[MemoryGuardian] Guardian STOPPED for critical task.");
        }

        public void Resume()
        {
            LocalMachineSettingsEngine.IsGuardianPaused = false;

            _highMemorySeconds = 0;
            _checkTimer?.Start();
            Debug.WriteLine("[MemoryGuardian] Guardian RESTORED.");
        }

        public void StartBackgroundSentry()
        {
            _isBackgroundMode = true;
            Debug.WriteLine("[MemoryGuardian] Switched to Background Mode (300MB Deep Clean limits active).");
        }

        public void StopBackgroundSentry()
        {
            _isBackgroundMode = false;
            _highMemorySeconds = 0;
            Debug.WriteLine("[MemoryGuardian] Switched to Active UI Mode (600MB Emergency limits active).");
        }
        #endregion

        #region Core Logic
        private ulong GetAccurateMemoryUsage(Process process)
        {
            try
            {
                if (_ramCounter != null)
                {
                    return (ulong)_ramCounter.NextValue();
                }
                return (ulong)process.PrivateMemorySize64;
            }
            catch
            {
                return (ulong)process.PrivateMemorySize64;
            }
        }

        private void CheckMemoryState()
        {
            if (LocalMachineSettingsEngine.IsGuardianPaused)
            {
                return;
            }

            using var currentProcess = Process.GetCurrentProcess();
            ulong privateUsage = GetAccurateMemoryUsage(currentProcess);

            if (_isBackgroundMode)
            {
                if (privateUsage > _currentThresholdBytes)
                {
                    _highMemorySeconds += TimerIntervalSeconds;
                    Debug.WriteLine($"[MemoryGuardian] Background Warning: RAM at {privateUsage / 1024 / 1024}MB for {_highMemorySeconds}s.");

                    if (_highMemorySeconds >= RequiredSustainedSeconds)
                    {
                        PerformDeepCleanup(currentProcess, privateUsage);
                        _highMemorySeconds = 0;
                    }
                }
                else if (_highMemorySeconds > 0)
                {
                    Debug.WriteLine("[MemoryGuardian] RAM dropped naturally. Canceling cleanup countdown.");
                    _highMemorySeconds = 0;
                }
            }
            else
            {
                if (privateUsage > _emergencyThresholdBytes)
                {
                    Debug.WriteLine($"[MemoryGuardian] EMERGENCY: Active RAM exceeded 700MB ({privateUsage / 1024 / 1024}MB). Initiating Gentle Trim...");
                    PerformGentleCleanup(currentProcess, privateUsage);
                }
            }
        }

        private void PerformDeepCleanup(Process currentProcess, ulong privateUsage)
        {
            ulong physicalBefore = (ulong)currentProcess.WorkingSet64;

            Debug.WriteLine($"[MemoryGuardian] Sustained background memory confirmed. Initiating Deep Cleanup...");

            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect();

            EmptyWorkingSet(currentProcess.Handle);

            currentProcess.Refresh();
            ulong physicalAfter = (ulong)currentProcess.WorkingSet64;

            Debug.WriteLine($"[MemoryGuardian] Deep Cleanup complete. Physical RAM dropped to: {physicalAfter / 1024 / 1024}MB");

            DispatchUpdate(physicalBefore, physicalAfter);
        }

        private void PerformGentleCleanup(Process currentProcess, ulong privateUsage)
        {
            ulong physicalBefore = (ulong)currentProcess.WorkingSet64;

            GC.Collect(2, GCCollectionMode.Optimized, false, false);

            currentProcess.Refresh();
            ulong physicalAfter = (ulong)currentProcess.WorkingSet64;

            Debug.WriteLine($"[MemoryGuardian] Gentle Trim complete. Physical RAM dropped to: {physicalAfter / 1024 / 1024}MB");

            DispatchUpdate(physicalBefore, physicalAfter);
        }

        public void ForceImmediateCleanup()
        {
            Debug.WriteLine("[MemoryGuardian] Manual forced cleanup requested (Tray Minimize).");
            using var currentProcess = Process.GetCurrentProcess();
            PerformDeepCleanup(currentProcess, GetAccurateMemoryUsage(currentProcess));
        }

        private void DispatchUpdate(ulong physicalBefore, ulong physicalAfter)
        {
            var dispatcher = MainWindow.Instance?.DispatcherQueue;
            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() =>
                {
                    _onCleanupPerformed?.Invoke(physicalBefore, physicalAfter);
                });
            }
            else
            {
                _onCleanupPerformed?.Invoke(physicalBefore, physicalAfter);
            }
        }
        #endregion

        #region Cleanup
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _ramCounter?.Dispose();

            GC.SuppressFinalize(this);
        }
        #endregion
    }
}