// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Events;

public class SettingsRefreshedEvent : IDomainEvent
{
    public DateTime Timestamp { get; }
    public Guid EventId { get; }
    public string SectionDisplayName { get; }

    public SettingsRefreshedEvent(string sectionDisplayName)
    {
        Timestamp = DateTime.UtcNow;
        EventId = Guid.NewGuid();
        SectionDisplayName = sectionDisplayName;
    }
}
