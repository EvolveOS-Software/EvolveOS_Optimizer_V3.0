// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EvolveOS_Optimizer.Core.Model
{
    public class DismissedEventOccurrence
    {
        public string EventId { get; set; } = "";
        public string SourceName { get; set; } = "";
        public string DateString { get; set; } = "";
        public string OriginalHash { get; set; } = "";
    }

    public class DismissedEventCard : INotifyPropertyChanged
    {
        public string OriginalHash { get; set; } = string.Empty;
        public string EventId { get; set; } = "";
        public string SourceName { get; set; } = "";

        public string LatestDateString { get; set; } = "";

        public string Message { get; set; } = "";
        public string FullMessage { get; set; } = "";

        public ObservableCollection<DismissedEventOccurrence> Occurrences { get; set; } = new ObservableCollection<DismissedEventOccurrence>();

        private string _occurrenceCount = string.Empty;
        public string OccurrenceCount
        {
            get => _occurrenceCount;
            set
            {
                if (_occurrenceCount != value)
                {
                    _occurrenceCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}