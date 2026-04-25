// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License. 

using System.Diagnostics.Eventing.Reader;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class EventLogMinerHelper
    {
        public async Task<List<SystemEventItem>> MineRecentErrorsAsync(int hoursToLookBack = 24, int maxRecords = 50)
        {
            var results = new List<SystemEventItem>();

            await Task.Run(() =>
            {
                long milliseconds = hoursToLookBack * 60 * 60 * 1000;
                string queryStr = $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= {milliseconds}]]]";

                string[] logsToMine = { "System", "Application" };

                foreach (var logName in logsToMine)
                {
                    try
                    {
                        var query = new EventLogQuery(logName, PathType.LogName, queryStr)
                        {
                            ReverseDirection = true
                        };

                        using var reader = new EventLogReader(query);
                        EventRecord record;

                        while ((record = reader.ReadEvent()) != null && results.Count < maxRecords)
                        {
                            using (record)
                            {
                                string rawDescription = record.FormatDescription() ?? "No description available.";

                                results.Add(new SystemEventItem
                                {
                                    TimeCreated = record.TimeCreated ?? DateTime.Now,
                                    SourceName = record.ProviderName,
                                    EventId = record.Id,
                                    Level = record.Level ?? 2,

                                    FullMessage = rawDescription,

                                    Message = CleanUpMessage(rawDescription)
                                });
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Debug.WriteLine($"[Event Miner] Access denied to {logName} log. Running without Admin privileges.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Event Miner] Error reading {logName}: {ex.Message}");
                    }
                }
            });

            results.Sort((a, b) => b.TimeCreated.CompareTo(a.TimeCreated));

            return results;
        }

        private string CleanUpMessage(string rawMessage)
        {
            string clean = rawMessage.Replace("\r", "").Replace("\n", " ");
            int firstPeriod = clean.IndexOf('.');
            if (firstPeriod > 0 && firstPeriod < 150)
            {
                return clean.Substring(0, firstPeriod + 1);
            }
            return clean.Length > 100 ? clean.Substring(0, 97) + "..." : clean;
        }
    }
}