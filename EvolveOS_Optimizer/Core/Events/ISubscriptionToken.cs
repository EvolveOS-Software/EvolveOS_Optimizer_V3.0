// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Events;

public interface ISubscriptionToken : IDisposable
{
    Guid SubscriptionId { get; }
    Type EventType { get; }
}
