// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.AdvApi32;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class ValuesViewerViewModel : ObservableObject
    {
        #region Properties
        private RegistrySnapshot? _snapshotA;
        private RegistrySnapshot? _snapshotB;
        private CancellationTokenSource _snapshotCts = new();
        #endregion

        #region Observable Properties
        [ObservableProperty]
        public partial KeyItem? SelectedKeyItem { get; set; }

        [ObservableProperty]
        public partial ValueItem? SelectedValueItem { get; set; }

        [ObservableProperty]
        public partial string StatusBarMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial GridLength ColumnName { get; set; } = new(256);

        [ObservableProperty]
        public partial GridLength ColumnType { get; set; } = new(144d);

        [ObservableProperty]
        public partial ObservableCollection<RegistryChange> SnapshotResults { get; set; } = new();

        [ObservableProperty]
        public partial bool IsSnapshotActive { get; set; }
        #endregion

        #region Collections
        [ObservableProperty]
        public partial ObservableCollection<ValueItem> ValueItems { get; set; }

        private readonly List<ValueItem> _allValueItems = new();

        private readonly ObservableCollection<BreadcrumbBarPathItem> _selectedKeyPathItems;
        public ReadOnlyObservableCollection<BreadcrumbBarPathItem> SelectedKeyPathItems { get; }

        [ObservableProperty]
        public partial ObservableCollection<RegistrySearchResult> SearchResults { get; set; } = new();

        [ObservableProperty]
        public partial bool IsSearchRunning { get; set; }

        [ObservableProperty]
        public partial bool IsSearchActive { get; set; }

        public RegistrySearchOptions SavedSearchOptions { get; } = new RegistrySearchOptions();

        private Brush _connectionStatusBrush = new SolidColorBrush(Colors.Gray);
        public Brush ConnectionStatusBrush
        {
            get => _connectionStatusBrush;
            set
            {
                _connectionStatusBrush = value;
                OnPropertyChanged();
            }
        }

        private bool _isAiReady;
        public bool IsAiReady
        {
            get => _isAiReady;
            set
            {
                _isAiReady = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Constructor
        public ValuesViewerViewModel()
        {
            ValueItems = new ObservableCollection<ValueItem>();

            _selectedKeyPathItems = new ObservableCollection<BreadcrumbBarPathItem>();
            SelectedKeyPathItems = new ReadOnlyObservableCollection<BreadcrumbBarPathItem>(_selectedKeyPathItems);

            InitializeBreadcrumbBarItems();
        }
        #endregion

        #region Public Methods
        public async Task<Win32Error> EnumerateRegistryValuesAsync(HKEY hRootKey, string subRoot)
        {
            var tempItems = new System.Collections.Generic.List<ValueItem>();
            ValueItem defaultItem = new();
            bool hasDefaultKey = false;

            var openResult = RegOpenKeyEx(
                hRootKey,
                subRoot,
                0,
                REGSAM.KEY_QUERY_VALUE | REGSAM.READ_CONTROL,
                out SafeRegistryHandle handle);

            if (openResult.Failed)
            {
                return openResult;
            }

            using (handle)
            {
                var queryResult = RegQueryInfoKey(handle, null, ref Unsafe.NullRef<uint>(), default, out _, out _, out _, out var cValues, out var cbMaxValueNameLen, out var cbMaxValueLen, out _, out _);

                if (queryResult.Failed)
                {
                    return queryResult;
                }

                tempItems.Capacity = (int)cValues;

                uint cchValueName;
                uint cbData;
                StringBuilder valueName;

                for (uint index = 0; index < cValues; index++)
                {
                    cchValueName = cbMaxValueNameLen + 4;
                    valueName = new StringBuilder((int)cchValueName);

                    cbData = Math.Max(cbMaxValueLen + (cbMaxValueLen % 2), 4);

                    using (SafeHGlobalHandle data = new SafeHGlobalHandle((int)cbData))
                    {
                        var enumResult = RegEnumValue(handle, index, valueName, ref cchValueName, default, out var type, data, ref cbData);

                        if (enumResult.Failed)
                        {
                            return enumResult;
                        }

                        ValueItem item = new()
                        {
                            Name = valueName.ToString(),
                            DisplayName = valueName.ToString(),
                            TypeString = type.ToString(),
                            DataSize = cbData,
                            Type = type,
                        };

                        if (string.IsNullOrEmpty(item.Name) && !hasDefaultKey)
                        {
                            defaultItem = new()
                            {
                                Name = valueName.ToString(),
                                DataSize = 0,
                                DisplayName = ResourceString.GetString("values_vm_default_name"),
                                IsRenamable = false,
                                DisplayValue = data.ToString(-1, CharSet.Auto) ?? string.Empty,
                                EditableValue = "",
                                Type = REG_VALUE_TYPE.REG_SZ,
                                TypeString = "REG_SZ",
                            };

                            hasDefaultKey = true;
                            continue;
                        }
                        else if (string.IsNullOrEmpty(item.Name))
                        {
                            continue;
                        }

                        switch (type)
                        {
                            case REG_VALUE_TYPE.REG_SZ:
                            case REG_VALUE_TYPE.REG_EXPAND_SZ:
                                {
                                    var value = data.ToString(-1, CharSet.Auto) ?? string.Empty;
                                    item.DisplayValue = value;
                                    item.EditableValue = item.DisplayValue;
                                }
                                break;

                            case REG_VALUE_TYPE.REG_BINARY:
                                {
                                    var value = data.ToArray<byte>((int)cbData) ?? Array.Empty<byte>();
                                    if (value.Length == 0)
                                    {
                                        item.DisplayValue = ResourceString.GetString("values_vm_zero_length_binary");
                                        item.EditableValue = "";
                                        break;
                                    }

                                    StringBuilder sb = new();
                                    foreach (var atom in value)
                                    {
                                        sb.AppendFormat("{0,2:x2} ", Convert.ToUInt32(atom));
                                    }

                                    item.DisplayValue = sb.ToString().TrimEnd();
                                    item.EditableValue = item.DisplayValue;
                                }
                                break;

                            case REG_VALUE_TYPE.REG_DWORD:
                                {
                                    item.TypeString = "REG_DWORD";
                                    var value = data.ToStructure<uint>();
                                    item.DisplayValue = string.Format("0x{0,8:x8} ({1})", value, value);
                                    item.EditableValue = value.ToString();
                                }
                                break;

                            case REG_VALUE_TYPE.REG_MULTI_SZ:
                                {
                                    var value = data.ToString(-1, CharSet.Auto) ?? string.Empty;
                                    foreach (var atom in value.Split('\n'))
                                    {
                                        item.DisplayValue += $"{atom} ";
                                    }
                                    item.DisplayValue = item.DisplayValue.TrimEnd();
                                    item.EditableValue = value;
                                }
                                break;

                            case REG_VALUE_TYPE.REG_QWORD:
                                {
                                    item.TypeString = "REG_QWORD";
                                    var value = data.ToStructure<ulong>();
                                    item.DisplayValue = string.Format("0x{0,16:x16} ({1})", value, value);
                                    item.EditableValue = value.ToString();
                                }
                                break;
                        }

                        tempItems.Add(item);
                    }
                }
            }

            if (!hasDefaultKey)
            {
                defaultItem = new()
                {
                    Name = "",
                    DataSize = 0,
                    DisplayName = ResourceString.GetString("values_vm_default_name"),
                    IsRenamable = false,
                    DisplayValue = ResourceString.GetString("values_vm_value_not_set"),
                    EditableValue = "",
                    Type = REG_VALUE_TYPE.REG_SZ,
                    TypeString = "REG_SZ",
                };
            }

            var sortedItems = tempItems.OrderBy(x => x.DisplayName).ToList();
            sortedItems.Insert(0, defaultItem);

            var tcs = new TaskCompletionSource<bool>();

            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                SetBreadcrumbBarItems(hRootKey, subRoot);

                ValueItems = new ObservableCollection<ValueItem>(sortedItems);

                tcs.SetResult(true);
            });

            await tcs.Task;

            return Win32Error.ERROR_SUCCESS;
        }

        public void SetBreadcrumbBarItems(HKEY hkey, string subRoot)
        {
            if (hkey == HKEY.HKEY_CLASSES_ROOT)
                _selectedKeyPathItems.Add(new() { PathItem = "HKEY_CLASSES_ROOT" });
            else if (hkey == HKEY.HKEY_CURRENT_CONFIG)
                _selectedKeyPathItems.Add(new() { PathItem = "HKEY_CURRENT_CONFIG" });
            else if (hkey == HKEY.HKEY_CURRENT_USER)
                _selectedKeyPathItems.Add(new() { PathItem = "HKEY_CURRENT_USER" });
            else if (hkey == HKEY.HKEY_LOCAL_MACHINE)
                _selectedKeyPathItems.Add(new() { PathItem = "HKEY_LOCAL_MACHINE" });
            else if (hkey == HKEY.HKEY_USERS)
                _selectedKeyPathItems.Add(new() { PathItem = "HKEY_USERS" });

            if (string.IsNullOrEmpty(subRoot) || subRoot.Split('\\').Length == 0)
            {
                _selectedKeyPathItems[^1].IsLast = true;
                return;
            }

            subRoot = subRoot.TrimEnd('\\');
            var items = subRoot.Split('\\');

            foreach (var item in items)
            {
                _selectedKeyPathItems.Add(new() { PathItem = item });
            }

            _selectedKeyPathItems[^1].IsLast = true;
        }

        public Win32Error SaveRegistryValue(KeyItem parentKey, ValueItem valueToSave)
        {
            if (parentKey == null || valueToSave == null) return Win32Error.ERROR_INVALID_PARAMETER;

            var openResult = RegOpenKeyEx(
                parentKey.RootHive,
                parentKey.Path,
                0,
                REGSAM.KEY_SET_VALUE,
                out SafeRegistryHandle phKey);

            if (openResult.Failed) return openResult;

            using (phKey)
            {
                try
                {
                    byte[] data;

                    switch (valueToSave.Type)
                    {
                        case REG_VALUE_TYPE.REG_SZ:
                        case REG_VALUE_TYPE.REG_EXPAND_SZ:
                            data = Encoding.Unicode.GetBytes(valueToSave.EditableValue + '\0');
                            break;
                        case REG_VALUE_TYPE.REG_DWORD:
                            if (uint.TryParse(valueToSave.EditableValue, out uint dwordVal))
                                data = BitConverter.GetBytes(dwordVal);
                            else
                                return Win32Error.ERROR_INVALID_DATA;
                            break;
                        case REG_VALUE_TYPE.REG_QWORD:
                            if (ulong.TryParse(valueToSave.EditableValue, out ulong qwordVal))
                                data = BitConverter.GetBytes(qwordVal);
                            else
                                return Win32Error.ERROR_INVALID_DATA;
                            break;
                        case REG_VALUE_TYPE.REG_MULTI_SZ:
                            var lines = valueToSave.EditableValue.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            string multiSzStr = string.Join("\0", lines) + "\0\0";
                            data = Encoding.Unicode.GetBytes(multiSzStr);
                            break;
                        case REG_VALUE_TYPE.REG_BINARY:
                            data = ParseBinaryString(valueToSave.EditableValue);
                            break;
                        default:
                            return Win32Error.ERROR_INVALID_DATATYPE;
                    }

                    string valueName = valueToSave.Name ?? string.Empty;

                    try
                    {
                        string hiveName = GetHiveNameFromHKEY(parentKey.RootHive);
                        RegistryKey? rootKey = GetRootKeyFromName(hiveName);

                        object? oldData = null;
                        RegistryValueKind oldKind = RegistryValueKind.Unknown;
                        TransactionAction action = TransactionAction.AddValue;

                        if (rootKey != null)
                        {
                            using (var rKey = rootKey.OpenSubKey(parentKey.Path, writable: false))
                            {
                                if (rKey != null && rKey.GetValueNames().Contains(valueName, StringComparer.OrdinalIgnoreCase))
                                {
                                    oldData = rKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                                    oldKind = rKey.GetValueKind(valueName);
                                    action = TransactionAction.ModifyValue;
                                }
                            }
                        }

                        RegistryValueKind newKind = ConvertToRegistryValueKind(valueToSave.Type);

                        RegistryTransactionManager.RecordTransaction(new RegistryTransaction
                        {
                            Action = action,
                            RootHiveName = hiveName,
                            SubKeyPath = parentKey.Path,
                            ValueName = valueName,
                            OldData = oldData,
                            NewData = valueToSave.EditableValue,
                            ValueKind = newKind == RegistryValueKind.Unknown ? oldKind : newKind
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Transaction Manager Error] {ex.Message}");
                    }

                    var setResult = RegSetValueEx(phKey, valueName, 0, valueToSave.Type, data, (uint)data.Length);

                    return setResult;
                }
                catch
                {
                    return Win32Error.ERROR_INTERNAL_ERROR;
                }
            }
        }

        public void RefreshCurrentKey()
        {
            if (SelectedKeyItem == null || SelectedKeyItem.SelectedRootComputer || SelectedKeyItem.RootHive.IsNull)
            {
                return;
            }

            StatusBarMessage = ResourceString.GetString("values_vm_refreshing_undo");

            Task.Run(async () =>
            {
                var result = await EnumerateRegistryValuesAsync(SelectedKeyItem.RootHive, SelectedKeyItem.Path);

                App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    if (result.Failed)
                    {
                        StatusBarMessage = string.Format(ResourceString.GetString("values_vm_refresh_failed"), result.FormatMessage());
                    }
                    else
                    {
                        StatusBarMessage = ResourceString.GetString("values_vm_undo_success");
                    }
                });
            });
        }
        #endregion

        #region Registry Snapshots
        public async Task RunFirstSnapshotAsync()
        {
            HKEY targetHive = HKEY.HKEY_CURRENT_USER;
            string targetPath = @"Software";

            StatusBarMessage = ResourceString.GetString("values_vm_snapshot_a_start");

            _snapshotA = await RegistrySnapshotEngine.TakeSnapshotAsync(targetHive, targetPath, (keysScanned) =>
            {
                App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    StatusBarMessage = string.Format(ResourceString.GetString("values_vm_snapshot_a_scan"), keysScanned);
                });
            }, _snapshotCts.Token);

            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                StatusBarMessage = ResourceString.GetString("values_vm_snapshot_a_complete");
            });
        }

        public async Task RunSecondSnapshotAndCompareAsync()
        {
            if (_snapshotA == null)
            {
                StatusBarMessage = ResourceString.GetString("values_vm_snapshot_b_error");
                return;
            }

            HKEY targetHive = HKEY.HKEY_CURRENT_USER;
            string targetPath = @"Software";

            StatusBarMessage = ResourceString.GetString("values_vm_snapshot_b_start");

            _snapshotB = await RegistrySnapshotEngine.TakeSnapshotAsync(targetHive, targetPath, (keysScanned) =>
            {
                App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    StatusBarMessage = string.Format(ResourceString.GetString("values_vm_snapshot_b_scan"), keysScanned);
                });
            }, _snapshotCts.Token);

            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                StatusBarMessage = ResourceString.GetString("values_vm_comparing_snapshots");
            });

            var changes = await RegistrySnapshotEngine.CompareSnapshotsAsync(_snapshotA, _snapshotB, _snapshotCts.Token);

            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                StatusBarMessage = string.Format(ResourceString.GetString("values_vm_compare_complete"), changes.Count);

                App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    StatusBarMessage = string.Format(ResourceString.GetString("values_vm_compare_complete"), changes.Count);

                    SnapshotResults.Clear();
                    foreach (var change in changes)
                    {
                        SnapshotResults.Add(change);
                    }

                    IsSnapshotActive = true;
                });
            });
        }
        #endregion

        #region Global Search Engine
        public async Task SearchRegistryAsync(CancellationToken token)
        {
            var options = SavedSearchOptions;
            if (string.IsNullOrWhiteSpace(options.Query)) return;

            IsSearchActive = true;
            IsSearchRunning = true;
            SearchResults.Clear();

            int matchCount = 0;

            App.MainWindow?.DispatcherQueue?.TryEnqueue(() => {
                StatusBarMessage = string.Format(ResourceString.GetString("values_vm_searching_registry"), options.Query);
            });

            var currentSelection = SelectedKeyItem;
            bool isComputerSelected = currentSelection == null || currentSelection.SelectedRootComputer;
            HKEY selectedHive = currentSelection?.RootHive ?? default;
            string startingPath = currentSelection?.Path ?? "";

            await Task.Run(() =>
            {
                try
                {
                    HKEY[] hivesToSearch = isComputerSelected
                        ? new HKEY[] { HKEY.HKEY_CLASSES_ROOT, HKEY.HKEY_CURRENT_USER, HKEY.HKEY_LOCAL_MACHINE, HKEY.HKEY_USERS, HKEY.HKEY_CURRENT_CONFIG }
                        : new HKEY[] { selectedHive };

                    foreach (var hive in hivesToSearch)
                    {
                        if (token.IsCancellationRequested) break;

                        string pathTarget = isComputerSelected ? "" : startingPath;

                        RecursiveGridSearch(hive, pathTarget, options, token, ref matchCount);
                    }

                    App.MainWindow?.DispatcherQueue?.TryEnqueue(() => {
                        StatusBarMessage = token.IsCancellationRequested
                            ? string.Format(ResourceString.GetString("values_vm_search_cancelled"), matchCount)
                            : string.Format(ResourceString.GetString("values_vm_search_finished"), matchCount);
                        IsSearchRunning = false;
                    });
                }
                catch (Exception ex)
                {
                    App.MainWindow?.DispatcherQueue?.TryEnqueue(() => {
                        StatusBarMessage = string.Format(ResourceString.GetString("values_vm_search_aborted"), ex.Message);
                        IsSearchRunning = false;
                    });
                }
            }, token);
        }

        private void RecursiveGridSearch(HKEY rootHive, string currentPath, RegistrySearchOptions options, CancellationToken token, ref int matchCount)
        {
            if (token.IsCancellationRequested) return;

            var openResult = RegOpenKeyEx(rootHive, currentPath, 0, REGSAM.KEY_QUERY_VALUE | REGSAM.KEY_ENUMERATE_SUB_KEYS, out var hKey);
            if (openResult.Failed) return;

            using (hKey)
            {
                if (options.SearchKeys && !string.IsNullOrEmpty(currentPath))
                {
                    string keyName = currentPath.Substring(currentPath.LastIndexOf('\\') + 1);
                    if (IsMatch(keyName, options.Query, options.MatchWholeString))
                    {
                        var result = new RegistrySearchResult
                        {
                            RootHive = rootHive,
                            FullPath = currentPath,
                            MatchType = "Key",
                            Name = keyName
                        };
                        AddResultToGrid(result, ref matchCount);
                    }
                }

                if (options.SearchValues || options.SearchData)
                {
                    uint index = 0;
                    uint cchValueName = 16383;
                    var valueName = new StringBuilder((int)cchValueName);
                    uint cbData = 0;

                    while (RegEnumValue(hKey, index, valueName, ref cchValueName, default, out var type, IntPtr.Zero, ref cbData).Succeeded)
                    {
                        if (token.IsCancellationRequested) return;

                        string vName = valueName.ToString();
                        bool valueMatched = false;
                        string dataStr = "";

                        if (options.SearchValues && IsMatch(vName, options.Query, options.MatchWholeString))
                        {
                            valueMatched = true;
                        }

                        if (options.SearchData && (type == REG_VALUE_TYPE.REG_SZ || type == REG_VALUE_TYPE.REG_EXPAND_SZ))
                        {
                            using var dataHandle = new SafeHGlobalHandle(Math.Max((int)cbData, 4));
                            uint tempSize = 16383;
                            var isolatedNameBuffer = new StringBuilder((int)tempSize);

                            if (RegEnumValue(hKey, index, isolatedNameBuffer, ref tempSize, default, out _, dataHandle, ref cbData).Succeeded)
                            {
                                dataStr = dataHandle.ToString(-1, CharSet.Auto) ?? "";
                                if (IsMatch(dataStr, options.Query, options.MatchWholeString))
                                {
                                    valueMatched = true;
                                }
                            }
                        }

                        if (valueMatched)
                        {
                            var result = new RegistrySearchResult
                            {
                                RootHive = rootHive,
                                FullPath = currentPath,
                                MatchType = "Value",
                                Name = string.IsNullOrEmpty(vName) ? ResourceString.GetString("values_vm_default_name") : vName,
                                Data = dataStr
                            };
                            AddResultToGrid(result, ref matchCount);
                        }

                        index++; cchValueName = 16383; valueName.Clear();
                    }
                }

                uint subKeyIndex = 0;
                uint cchKeyName = 256;
                var subKeyName = new StringBuilder((int)cchKeyName);

                while (RegEnumKeyEx(hKey, subKeyIndex, subKeyName, ref cchKeyName, default, null, ref Unsafe.NullRef<uint>(), out _).Succeeded)
                {
                    string nextPath = string.IsNullOrEmpty(currentPath) ? subKeyName.ToString() : $"{currentPath}\\{subKeyName}";

                    RecursiveGridSearch(rootHive, nextPath, options, token, ref matchCount);

                    subKeyIndex++; cchKeyName = 256; subKeyName.Clear();
                }
            }
        }

        private void AddResultToGrid(RegistrySearchResult result, ref int matchCount)
        {
            matchCount++;
            var currentCount = matchCount;

            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                SearchResults.Add(result);

                if (currentCount % 10 == 0)
                {
                    StatusBarMessage = string.Format(ResourceString.GetString("values_vm_searching_found"), currentCount);
                }
            });
        }

        private bool IsMatch(string target, string query, bool matchWholeString)
        {
            if (string.IsNullOrEmpty(target)) return false;

            if (matchWholeString)
                return target.Equals(query, StringComparison.OrdinalIgnoreCase);
            else
                return target.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region Partial Methods
        partial void OnSelectedKeyItemChanged(KeyItem? oldValue, KeyItem? newValue)
        {
            ValueItems.Clear();
            InitializeBreadcrumbBarItems();

            if (newValue == null) return;

            if (newValue.SelectedRootComputer || newValue.RootHive.IsNull)
            {
                StatusBarMessage = ResourceString.GetString("values_vm_ready");
                return;
            }

            StatusBarMessage = ResourceString.GetString("values_vm_loading_values");

            Task.Run(async () =>
            {
                var result = await EnumerateRegistryValuesAsync(newValue.RootHive, newValue.Path);

                App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    if (result.Failed)
                    {
                        StatusBarMessage = string.Format(ResourceString.GetString("values_vm_load_keys_failed"), result.FormatMessage());
                    }
                    else
                    {
                        StatusBarMessage = ResourceString.GetString("values_vm_ready");
                    }
                });
            });
        }
        #endregion

        #region Private/Helper Methods
        private void InitializeBreadcrumbBarItems()
        {
            _selectedKeyPathItems.Clear();
            _selectedKeyPathItems.Add(new BreadcrumbBarPathItem() { PathItem = "Computer" });
        }

        private byte[] ParseBinaryString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<byte>();

            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var bytes = new byte[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                bytes[i] = Convert.ToByte(parts[i], 16);
            }

            return bytes;
        }

        private string GetHiveNameFromHKEY(HKEY hkey)
        {
            if (hkey == HKEY.HKEY_CLASSES_ROOT) return "HKEY_CLASSES_ROOT";
            if (hkey == HKEY.HKEY_CURRENT_USER) return "HKEY_CURRENT_USER";
            if (hkey == HKEY.HKEY_LOCAL_MACHINE) return "HKEY_LOCAL_MACHINE";
            if (hkey == HKEY.HKEY_USERS) return "HKEY_USERS";
            if (hkey == HKEY.HKEY_CURRENT_CONFIG) return "HKEY_CURRENT_CONFIG";
            return "UNKNOWN";
        }

        private RegistryKey? GetRootKeyFromName(string rootName)
        {
            return rootName switch
            {
                "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
                "HKEY_CURRENT_USER" => Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                "HKEY_USERS" => Registry.Users,
                "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
                _ => null
            };
        }

        private RegistryValueKind ConvertToRegistryValueKind(REG_VALUE_TYPE type)
        {
            return type switch
            {
                REG_VALUE_TYPE.REG_SZ => RegistryValueKind.String,
                REG_VALUE_TYPE.REG_EXPAND_SZ => RegistryValueKind.ExpandString,
                REG_VALUE_TYPE.REG_BINARY => RegistryValueKind.Binary,
                REG_VALUE_TYPE.REG_DWORD => RegistryValueKind.DWord,
                REG_VALUE_TYPE.REG_MULTI_SZ => RegistryValueKind.MultiString,
                REG_VALUE_TYPE.REG_QWORD => RegistryValueKind.QWord,
                _ => RegistryValueKind.Unknown
            };
        }
        #endregion
    }
}