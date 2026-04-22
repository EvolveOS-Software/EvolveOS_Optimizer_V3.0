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
                // This XPath string tells Windows: "Give me Level 1, 2, or 3 events from the last X hours"
                long milliseconds = hoursToLookBack * 60 * 60 * 1000;
                string queryStr = $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= {milliseconds}]]]";

                // Try System log first, then Application log
                string[] logsToMine = { "System", "Application" };

                foreach (var logName in logsToMine)
                {
                    try
                    {
                        var query = new EventLogQuery(logName, PathType.LogName, queryStr)
                        {
                            ReverseDirection = true // Newest first
                        };

                        using var reader = new EventLogReader(query);
                        EventRecord record;

                        while ((record = reader.ReadEvent()) != null && results.Count < maxRecords)
                        {
                            using (record)
                            {
                                results.Add(new SystemEventItem
                                {
                                    TimeCreated = record.TimeCreated ?? DateTime.Now,
                                    SourceName = record.ProviderName,
                                    EventId = record.Id,
                                    Level = record.Level ?? 2,

                                    // Windows Event messages can be massively long paragraphs. 
                                    // We grab just the first sentence for the UI dashboard.
                                    Message = CleanUpMessage(record.FormatDescription() ?? "No description available.")
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

            // Sort so the absolute newest events are at the top, regardless of which log they came from
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