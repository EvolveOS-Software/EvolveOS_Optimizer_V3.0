// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Events;

public class TooltipUpdatedEvent : IDomainEvent
{
    public DateTime Timestamp { get; }
    public Guid EventId { get; }
    public string SettingId { get; }
    public SettingTooltipData TooltipData { get; }
    public TooltipUpdatedEvent(string settingId, SettingTooltipData tooltipData)
    {
        Timestamp = DateTime.UtcNow;
        EventId = Guid.NewGuid();
        SettingId = settingId ?? throw new ArgumentNullException(nameof(settingId));
        TooltipData = tooltipData ?? throw new ArgumentNullException(nameof(tooltipData));
    }
}
