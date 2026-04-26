// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.Eventing.Reader;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class LiveEventWatcherHelper : IDisposable
    {
        private readonly List<EventLogWatcher> _watchers = new();
        private readonly Action<SystemEventItem> _onEventDetected;

        private readonly ConcurrentDictionary<string, DateTime> _eventDebouncer = new();
        private readonly int _debounceSeconds = 5;

        private readonly HashSet<int> _fixableEventIds = new()
        {
            1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
            31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 44, 45, 47, 49, 50, 51, 52, 54, 55, 56, 57, 58, 59, 60, 63, 65, 69,
            98, 100, 101, 102, 103, 107, 109, 110, 117, 123, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141,
            142, 143, 144, 153, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 201, 202, 300, 301, 302, 303, 304, 305, 306,
            307, 308, 310, 315, 316, 317, 400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411, 417, 418, 419, 420, 421,
            422, 423, 424, 425, 426, 427, 441, 442, 447, 448, 451, 454, 455, 467, 474, 477, 481, 482, 483, 488, 489, 490, 491,
            492, 493, 504, 505, 506, 507, 510, 512, 513, 514, 515, 523, 524, 525, 533, 566, 601, 603, 604, 800, 801, 804, 805,
            806, 808, 809, 810, 1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010, 1011, 1012, 1013, 1014, 1015,
            1016, 1017, 1018, 1019, 1020, 1021, 1022, 1023, 1024, 1025, 1030, 1033, 1040, 1041, 1042, 1053, 1054, 1055, 1058,
            1074, 1076, 1096, 1101, 1102, 1104, 1105, 1108, 1112, 1116, 1117, 1118, 1119, 1500, 1501, 1502, 1504, 1505, 1506,
            1507, 1508, 1509, 1511, 1512, 1513, 1514, 1515, 1517, 1530, 1531, 1532, 1534, 1542, 2000, 2001, 2002, 2003, 2004,
            2005, 2010, 2011, 2012, 2021, 2022, 2049, 2050, 2100, 2101, 2102, 2504, 2505, 2506, 2507, 2508, 2509, 3000, 3001,
            3002, 3003, 3004, 3006, 3007, 4004, 4005, 4007, 4008, 4101, 4109, 4115, 4226, 4227, 4231, 4319, 4624, 4625, 4634,
            4647, 4648, 4672, 4688, 4689, 4720, 4722, 4723, 4724, 4725, 4726, 4732, 4733, 4735, 4738, 4740, 4741, 4742, 4743,
            4744, 4745, 4746, 4747, 4748, 4749, 4750, 4800, 4801, 4802, 4803, 5000, 5001, 5002, 5003, 5004, 5005, 5006, 5007,
            5010, 5011, 5012, 5032, 5140, 5142, 5145, 5719, 6005, 6006, 6008, 6009, 6062, 6272, 6273, 6278, 7000, 7001, 7002,
            7003, 7004, 7005, 7006, 7009, 7011, 7022, 7023, 7024, 7026, 7030, 7031, 7032, 7034, 7035, 7036, 7040, 7042, 7045,
            7046, 7047, 7048, 7049, 7050, 7051, 7052, 8000, 8001, 8002, 8003, 8004, 8021, 8033, 8193, 8194, 8213, 8217, 8218,
            8219, 8220, 8221, 8222, 8223, 8224, 8225, 8226, 9000, 9001, 10000, 10001, 10002, 10005, 10010, 10011, 10012, 10013,
            10014, 10015, 10016, 10020, 10053, 10054, 10060, 10061, 10065, 10066, 10067, 10068, 10069, 10070, 10071, 10072,
            10073, 10074, 10100, 10101, 10102, 10103, 10104, 10105, 10106, 10107, 10108, 10109, 10110, 10111, 10112, 10113,
            10114, 10115, 10116, 10117, 10118, 10119, 10120, 10200, 10400, 11001, 11002, 11004, 11005, 11006, 11706, 11707,
            11708, 11724, 11728, 12001, 12010, 12011, 12012, 12013, 12289, 12290, 12291, 12292, 12293, 12294, 12295, 12296,
            12297, 12298, 12300, 12301, 12302, 12303, 12304, 36870, 36871, 36874, 36880, 36881, 36882, 36884, 36885, 36886,
            36887, 36888, 40961, 40962,

            9101, 9102, 9103, 9104, 9105, 9106, 9107, 9108, 9109,
            9110, 9111, 9112, 9113, 9114, 9115, 9116, 9117,

            9002, 9003, 1801
        };

        private readonly string[] _ignoredSources = {
            "MSBuild",
            "DistributedCOM",
            "Security-SPP",
            "Kernel-Processor-Power",
            "BTHUSB",
            "WLAN-AutoConfig",
            "ServiceHub",
            "VBCSCompiler",
            "devenv"
        };

        public LiveEventWatcherHelper(Action<SystemEventItem> onEventDetected)
        {
            _onEventDetected = onEventDetected;
        }

        public void Start()
        {
            string queryStr = "*[System[(Level=1 or Level=2 or Level=3)]]";

            string[] logsToWatch = {
                "System",
                "Application",
                "Microsoft-Windows-WindowsUpdateClient/Operational"
            };

            foreach (var logName in logsToWatch)
            {
                try
                {
                    var query = new EventLogQuery(logName, PathType.LogName, queryStr);
                    var watcher = new EventLogWatcher(query);

                    watcher.EventRecordWritten += OnEventRecordWritten;
                    watcher.Enabled = true;

                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LiveWatcher] Could not bind to log channel '{logName}': {ex.Message}");
                }
            }
        }

        private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
        {
            if (e.EventRecord == null) return;

            using (var record = e.EventRecord)
            {
                int eventId = record.Id;
                string source = record.ProviderName ?? "Unknown";
                byte level = (byte)(record.Level ?? 2);

                bool isNoisy = _ignoredSources.Any(s => source.Contains(s, StringComparison.OrdinalIgnoreCase));
                if (isNoisy && level > 1)
                {
                    return;
                }

                string eventFingerprint = eventId >= 9101
                    ? $"{eventId}_{source}_SECURE"
                    : $"{eventId}_{source}_{(record.TimeCreated?.Ticks ?? DateTime.Now.Ticks)}";

                if (LocalMachineSettingsEngine.DismissedEventsList.Contains(eventFingerprint))
                {
                    return;
                }

                string eventHash = $"{eventId}_{source}";
                if (_eventDebouncer.TryGetValue(eventHash, out DateTime lastSeen))
                {
                    if ((DateTime.Now - lastSeen).TotalSeconds < _debounceSeconds)
                    {
                        return;
                    }
                }
                _eventDebouncer[eventHash] = DateTime.Now;

                string rawDescription = record.FormatDescription() ??
                                        ResourceString.GetString("live_watcher_pending") ??
                                        "Live Interception: Detailed logs pending...";

                var newItem = new SystemEventItem
                {
                    TimeCreated = record.TimeCreated ?? DateTime.Now,
                    SourceName = source,
                    EventId = eventId,
                    Level = level,
                    FullMessage = rawDescription,
                    Message = CleanUpMessage(rawDescription)
                };

                if (_fixableEventIds.Contains(eventId))
                {
                    newItem.IsFixable = true;
                }

                _onEventDetected?.Invoke(newItem);
            }
        }

        private string CleanUpMessage(string rawMessage)
        {
            string clean = rawMessage.Replace("\r", "").Replace("\n", " ").Trim();
            return clean.Length > 150 ? clean.Substring(0, 147) + "..." : clean;
        }

        public void Dispose()
        {
            foreach (var watcher in _watchers)
            {
                watcher.Enabled = false;
                watcher.EventRecordWritten -= OnEventRecordWritten;
                watcher.Dispose();
            }
            _watchers.Clear();
            _eventDebouncer.Clear();
        }
    }
}