// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;

namespace EvolveOS_Optimizer.Core.Model
{
    public class DismissedEventOccurrence
    {
        public string EventId { get; set; } = "";
        public string SourceName { get; set; } = "";
        public string DateString { get; set; } = "";
        public string OriginalHash { get; set; } = "";
    }

    public class DismissedEventCard
    {
        public string EventId { get; set; } = "";
        public string SourceName { get; set; } = "";

        public string LatestDateString { get; set; } = "";

        public string Message { get; set; } = "";
        public string FullMessage { get; set; } = "";

        public ObservableCollection<DismissedEventOccurrence> Occurrences { get; set; } = new ObservableCollection<DismissedEventOccurrence>();

        public int OccurrenceCount => Occurrences.Count;
    }
}