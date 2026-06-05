// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Events;

public class FilterStateChangedEvent : IDomainEvent
{
    public DateTime Timestamp { get; }
    public Guid EventId { get; }
    public bool IsFilterEnabled { get; }

    public FilterStateChangedEvent(bool isFilterEnabled)
    {
        Timestamp = DateTime.UtcNow;
        EventId = Guid.NewGuid();
        IsFilterEnabled = isFilterEnabled;
    }
}
