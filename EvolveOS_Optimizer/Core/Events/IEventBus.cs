// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Events;

public interface IEventBus
{
    void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
    ISubscriptionToken Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;
    ISubscriptionToken SubscribeAsync<TEvent>(Func<TEvent, Task> handler) where TEvent : IDomainEvent;
    void Unsubscribe(ISubscriptionToken token);
}
