// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Text.Json;
using System.Threading;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Extensions;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class ScheduledCleanService
    {
        private static ScheduledCleanService? _instance;
        public static ScheduledCleanService Instance => _instance ??= new ScheduledCleanService();

        private PeriodicTimer? _timer;
        private DateTime _lastRunDate = DateTime.MinValue;
        private bool _isRunning;

        private ScheduledCleanService() { }

        public void Start()
        {
            if (_timer != null) return;

            _timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            _ = RunLoopAsync();
        }

        private async Task RunLoopAsync()
        {
            while (await _timer!.WaitForNextTickAsync())
            {
                if (!SettingsEngine.IsScheduledCleanEnabled || _isRunning)
                    continue;

                var now = DateTime.Now;

                if (_lastRunDate.Date == now.Date)
                    continue;

                if (!IsTodayScheduledDay(now.DayOfWeek))
                    continue;

                if (now.TimeOfDay >= SettingsEngine.ScheduledCleanTime)
                {
                    _isRunning = true;
                    await ExecuteSilentCleanAsync();

                    _lastRunDate = now;
                    _isRunning = false;
                }
            }
        }

        private bool IsTodayScheduledDay(DayOfWeek today)
        {
            int scheduleIndex = SettingsEngine.ScheduledCleanDayIndex;

            if (scheduleIndex == 0) return true;

            int mappedToday = today == DayOfWeek.Sunday ? 7 : (int)today;

            return scheduleIndex == mappedToday;
        }

        private async Task ExecuteSilentCleanAsync()
        {
            try
            {
                string targetPath = SettingsEngine.CustomWinapp2Path ?? Path.Combine(AppContext.BaseDirectory, "Winapp2.ini");
                if (!File.Exists(targetPath)) return;

                var parser = new Winapp2Parser();
                var detection = new DetectionService();
                var cleaner = new CleaningService();

                var allEntries = await parser.ParseFileAsync(targetPath);
                var installedEntries = allEntries.Where(detection.IsInstalled).ToList();

                var selectedNames = SettingsEngine.SelectedCleanerEntries;
                if (selectedNames.Count == 0) return;

                var entriesToClean = installedEntries.Where(e => selectedNames.Contains(e.Name)).ToList();
                if (entriesToClean.Count == 0) return;

                long totalFreedBytes = 0;
                int totalFilesRemoved = 0;

                foreach (var entry in entriesToClean)
                {
                    var result = await cleaner.AnalyzeAsync(entry, new Progress<string>());
                    var (removed, freed) = await cleaner.CleanAsync(result, new Progress<string>());

                    totalFilesRemoved += removed;
                    totalFreedBytes += freed;
                }

                if (totalFreedBytes > 0)
                {
                    await RecordToHistoryAsync(totalFreedBytes);

                    ShowCompletionToast(totalFreedBytes.FormatBytes());
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        private async Task RecordToHistoryAsync(long bytesRecovered)
        {
            string historyFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvolveOS", "cleanup_history.json");
            var history = new List<CleaningSession>();

            if (File.Exists(historyFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(historyFilePath);
                    history = JsonSerializer.Deserialize<List<CleaningSession>>(json) ?? new List<CleaningSession>();
                }
                catch { }
            }

            history.Add(new CleaningSession(DateTime.Now, bytesRecovered));

            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            history = history.Where(x => x.Timestamp >= thirtyDaysAgo).ToList();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(historyFilePath)!);
                await File.WriteAllTextAsync(historyFilePath, JsonSerializer.Serialize(history));
            }
            catch { }
        }

        private void ShowCompletionToast(string freedSize)
        {
            string title = ResourceString.GetString("cleanup_schedule_toast_title");
            string descFormat = ResourceString.GetString("cleanup_schedule_toast_desc");
            string message = string.Format(descFormat, freedSize);

            NotificationManager.Show(title, message)
                               .WithSeverity(NotificationManager.NoticeSeverity.Success)
                               .Create();
        }
    }
}