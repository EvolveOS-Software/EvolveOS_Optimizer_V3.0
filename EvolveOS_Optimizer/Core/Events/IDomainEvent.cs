// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;

namespace EvolveOS_Optimizer.Core.Events;

public interface IDomainEvent
{
    DateTime Timestamp { get; }
    Guid EventId { get; }
}
