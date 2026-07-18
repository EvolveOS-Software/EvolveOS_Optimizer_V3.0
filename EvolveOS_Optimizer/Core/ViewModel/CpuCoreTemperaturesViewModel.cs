// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Dispatching;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class CoreGroup : ObservableCollection<CpuCoreInfo>
    {
        public string Key { get; }
        public CoreGroup(string key, IEnumerable<CpuCoreInfo> items) : base(items)
        {
            Key = key;
        }
    }

    public class TimeOption
    {
        public string Label { get; set; } = string.Empty;
        public int Seconds { get; set; }
    }

    public partial class CpuCoreTemperaturesViewModel : ObservableObject
    {
        #region Fields

        private bool _isPolling;
        private readonly DispatcherQueue _dispatcherQueue;
        private bool _isShowingTemperatures = false;
        private bool _needsRebuild = false;
        private bool _isLoading = true;

        #endregion

        #region Properties

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(IsNotLoading));
                }
            }
        }
        public bool IsNotLoading => !IsLoading;

        public bool IsShowingTemperatures
        {
            get => _isShowingTemperatures;
            set
            {
                if (SetProperty(ref _isShowingTemperatures, value))
                {
                    _needsRebuild = true;
                }
            }
        }

        public List<TimeOption> TimeOptions { get; } = new List<TimeOption>
        {
            new TimeOption { Label = "60 Seconds", Seconds = 60 },
            new TimeOption { Label = "5 Minutes", Seconds = 300 },
            new TimeOption { Label = "10 Minutes", Seconds = 600 },
            new TimeOption { Label = "15 Minutes", Seconds = 900 }
        };

        private TimeOption _selectedTimeOption;
        public TimeOption SelectedTimeOption
        {
            get => _selectedTimeOption;
            set
            {
                if (SetProperty(ref _selectedTimeOption, value) && value != null)
                {
                    foreach (var group in GroupedCores)
                    {
                        foreach (var core in group)
                        {
                            core.UpdateHistoryDuration(value.Seconds);
                        }
                    }
                }
            }
        }

        public ObservableCollection<CoreGroup> GroupedCores { get; } = new();

        #endregion

        #region Constructor

        public CpuCoreTemperaturesViewModel()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _selectedTimeOption = TimeOptions[0];
        }

        #endregion

        #region Initialization

        public async Task InitializeAsync()
        {
            IsLoading = true;

            var initialData = await Task.Run(() =>
            {
                HardwareTemperatureService.Instance.UpdateSensors();

                var data = _isShowingTemperatures
                    ? HardwareTemperatureService.Instance.GetCpuCoreTemperatures()
                    : HardwareTemperatureService.Instance.GetCpuCoreLoads();

                return data ?? new Dictionary<string, float>();
            });

            BuildGroupedCores(initialData);

            IsLoading = false;

            StartPolling();
        }

        #endregion

        #region Polling Control

        public void StartPolling()
        {
            if (_isPolling) return;
            _isPolling = true;

            Task.Run(async () =>
            {
                while (_isPolling)
                {
                    var startTime = DateTime.UtcNow;

                    HardwareTemperatureService.Instance.UpdateSensors();

                    bool fetchedAsTemp = _isShowingTemperatures;

                    var currentData = _isShowingTemperatures
                        ? HardwareTemperatureService.Instance.GetCpuCoreTemperatures()
                        : HardwareTemperatureService.Instance.GetCpuCoreLoads();

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (fetchedAsTemp != _isShowingTemperatures)
                        {
                            return;
                        }

                        if (_needsRebuild)
                        {
                            HandleToggleRebuild(currentData);
                            _needsRebuild = false;
                        }
                        else
                        {
                            UpdateCoreUI(currentData);
                        }
                    });

                    // Keep but disabled previous logic
                    /* var elapsedMilliseconds = (DateTime.UtcNow - startTime).TotalMilliseconds;
 
                    // 250 = 4 updates per second (Set for smooth animations)
                    // 333 = 3 updates per second
                    // 500 = 2 updates per second
                    // 1000 = 1 update per second (Default)

                    var timeToWait = 250 - (int)elapsedMilliseconds;

                    if (timeToWait > 0)
                    {
                        await Task.Delay(timeToWait);
                    }*/

                    await Task.Delay(150);
                }
            });
        }

        public void StopPolling()
        {
            _isPolling = false;
        }

        #endregion

        #region Data Processing & UI Sync

        private void UpdateCoreUI(Dictionary<string, float> currentData)
        {
            foreach (var group in GroupedCores)
            {
                foreach (var core in group)
                {
                    if (currentData.TryGetValue(core.SensorHardwareName, out float newValue))
                    {
                        if (!core.IsShowingLoad && newValue <= 0)
                        {
                            core.AddSensorRecord(core.SensorValue > 0 ? core.SensorValue : 35f);
                        }
                        else
                        {
                            core.AddSensorRecord(newValue);
                        }
                    }
                    else
                    {
                        if (core.IsShowingLoad)
                        {
                            core.AddSensorRecord(0f);
                        }
                        else
                        {
                            core.AddSensorRecord(core.SensorValue > 0 ? core.SensorValue : 35f);
                        }
                    }
                }
            }
        }

        private void HandleToggleRebuild(Dictionary<string, float> currentData)
        {
            var sortedData = currentData
                .OrderBy(kvp => GetCoreNumber(kvp.Key))
                .ThenBy(kvp => kvp.Key)
                .ToList();

            var pCoresRaw = sortedData.Where(kvp => !IsEfficiencyCore(kvp.Key)).ToList();
            var eCoresRaw = sortedData.Where(kvp => IsEfficiencyCore(kvp.Key)).ToList();

            bool structureChanged = GroupedCores.Count == 0 ||
                                    (GroupedCores.Count >= 1 && pCoresRaw.Count != GroupedCores[0].Count) ||
                                    (GroupedCores.Count >= 2 && eCoresRaw.Count != GroupedCores[1].Count);

            if (structureChanged)
            {
                GroupedCores.Clear();
                BuildGroupedCores(currentData);
                return;
            }

            bool isLoadView = !_isShowingTemperatures;

            // Update P-Cores
            if (GroupedCores.Count >= 1)
            {
                for (int i = 0; i < pCoresRaw.Count; i++)
                {
                    float val = pCoresRaw[i].Value;
                    if (!isLoadView && val <= 0) val = 35f;
                    GroupedCores[0][i].SwitchMode(isLoadView, pCoresRaw[i].Key, val);
                }
            }

            // Update E-Cores
            if (GroupedCores.Count >= 2)
            {
                for (int i = 0; i < eCoresRaw.Count; i++)
                {
                    float val = eCoresRaw[i].Value;
                    if (!isLoadView && val <= 0) val = 35f;
                    GroupedCores[1][i].SwitchMode(isLoadView, eCoresRaw[i].Key, val);
                }
            }
        }

        private int GetCoreNumber(string name)
        {
            var match = Regex.Match(name ?? string.Empty, @"\d+");
            return match.Success && int.TryParse(match.Value, out int result) ? result : 999;
        }

        private void BuildGroupedCores(Dictionary<string, float> data)
        {
            var sortedData = data
                .OrderBy(kvp => GetCoreNumber(kvp.Key))
                .ThenBy(kvp => kvp.Key)
                .ToList();

            var pCoresRaw = sortedData.Where(kvp => !IsEfficiencyCore(kvp.Key)).ToList();
            var eCoresRaw = sortedData.Where(kvp => IsEfficiencyCore(kvp.Key)).ToList();

            var regex = new Regex(@"\d+");
            bool isLoadView = !this.IsShowingTemperatures;

            int currentHistorySeconds = SelectedTimeOption?.Seconds ?? 60;

            var pCores = new List<CpuCoreInfo>();
            int pIndex = 1;
            foreach (var kvp in pCoresRaw)
            {
                string displayName = regex.Replace(kvp.Key, pIndex.ToString(), 1);

                float initialVal = kvp.Value;
                if (!isLoadView && initialVal <= 0) initialVal = 35f;

                pCores.Add(new CpuCoreInfo(isLoadView, currentHistorySeconds)
                {
                    CoreName = displayName,
                    SensorHardwareName = kvp.Key,
                    SensorValue = initialVal
                });
                pIndex++;
            }

            var eCores = new List<CpuCoreInfo>();
            int eIndex = 1;
            foreach (var kvp in eCoresRaw)
            {
                string displayName = regex.Replace(kvp.Key, eIndex.ToString(), 1);

                float initialVal = kvp.Value;
                if (!isLoadView && initialVal <= 0) initialVal = 35f;

                eCores.Add(new CpuCoreInfo(isLoadView, currentHistorySeconds)
                {
                    CoreName = displayName,
                    SensorHardwareName = kvp.Key,
                    SensorValue = initialVal
                });
                eIndex++;
            }

            if (pCores.Any())
            {
                string pHeader = ResourceString.GetString("key_performance_cores") ?? "Performance Cores";
                GroupedCores.Add(new CoreGroup(pHeader, pCores));
            }

            if (eCores.Any())
            {
                string eHeader = ResourceString.GetString("key_efficiency_cores") ?? "Efficiency Cores";
                GroupedCores.Add(new CoreGroup(eHeader, eCores));
            }
        }

        private bool IsEfficiencyCore(string coreName)
        {
            if (string.IsNullOrEmpty(coreName)) return false;

            if (coreName.Contains("E-Core", StringComparison.OrdinalIgnoreCase) ||
                coreName.Contains("Efficiency", StringComparison.OrdinalIgnoreCase) ||
                coreName.Contains("e core", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int pCoreCount = CpuCoreHelper.GetPerformanceCoreCount();
            int coreNumber = GetCoreNumber(coreName);

            return coreNumber > pCoreCount && coreNumber != 999;
        }

        #endregion

        #region Win32 Processor Topology Interop

        public static class CpuCoreHelper
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool GetLogicalProcessorInformationEx(int RelationshipType, IntPtr Buffer, ref int ReturnedLength);

            private static int? _pCoreCount = null;

            public static int GetPerformanceCoreCount()
            {
                if (_pCoreCount.HasValue) return _pCoreCount.Value;

                try
                {
                    int returnLength = 0;
                    GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref returnLength);

                    if (returnLength > 0)
                    {
                        IntPtr buffer = Marshal.AllocHGlobal(returnLength);
                        try
                        {
                            if (GetLogicalProcessorInformationEx(0, buffer, ref returnLength))
                            {
                                var efficiencyClasses = new List<byte>();
                                IntPtr currentPtr = buffer;
                                long endPtr = buffer.ToInt64() + returnLength;

                                while (currentPtr.ToInt64() < endPtr)
                                {
                                    int relationship = Marshal.ReadInt32(currentPtr);
                                    int size = Marshal.ReadInt32(currentPtr, 4);

                                    if (relationship == 0)
                                    {
                                        byte efficiencyClass = Marshal.ReadByte(currentPtr, 9);
                                        efficiencyClasses.Add(efficiencyClass);
                                    }

                                    currentPtr = new IntPtr(currentPtr.ToInt64() + size);
                                }

                                if (efficiencyClasses.Count > 0)
                                {
                                    byte maxEfficiency = efficiencyClasses.Max();
                                    byte minEfficiency = efficiencyClasses.Min();

                                    _pCoreCount = maxEfficiency == minEfficiency
                                        ? efficiencyClasses.Count
                                        : efficiencyClasses.Count(e => e == maxEfficiency);

                                    return _pCoreCount.Value;
                                }
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                    }
                }
                catch
                {
                    // Fail silently on native execution faults
                }

                _pCoreCount = 8;
                return _pCoreCount.Value;
            }
        }

        #endregion
    }
}