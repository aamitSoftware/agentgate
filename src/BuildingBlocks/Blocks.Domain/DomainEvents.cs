namespace Blocks.Domain;

/// <summary>In-process event raised by an aggregate. Never crosses a service boundary (integration events do).</summary>
public interface IDomainEvent;

/// <summary>
/// Handlers run inside SaveChanges, before the commit, so whatever they add to the same DbContext
/// (e.g. outbox messages) commits atomically with the change that raised the event.
/// Handlers must not call SaveChanges themselves, and should read state from the aggregate the event
/// carries — not re-query it: rows are not inserted yet, so a query can't see a newly added aggregate.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
