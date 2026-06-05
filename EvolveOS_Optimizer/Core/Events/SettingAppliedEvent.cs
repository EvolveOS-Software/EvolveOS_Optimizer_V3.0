// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Events;

public class SettingAppliedEvent : IDomainEvent
{
    public DateTime Timestamp { get; }
    public Guid EventId { get; }
    public string SettingId { get; }
    public bool IsEnabled { get; }
    public object? Value { get; }

    public SettingAppliedEvent(string settingId, bool isEnabled, object? value = null)
    {
        Timestamp = DateTime.UtcNow;
        EventId = Guid.NewGuid();
        SettingId = settingId ?? throw new ArgumentNullException(nameof(settingId));
        IsEnabled = isEnabled;
        Value = value;
    }
}
