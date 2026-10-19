using System.Collections.Concurrent;
using System.Reflection;
using Blocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Blocks.EntityFrameworkCore;

/// <summary>
/// Dispatches domain events while EF is *saving* (before the commit), not after it.
/// Anything a handler adds to the same DbContext — typically MassTransit bus-outbox rows — is
/// written in the same transaction as the change that raised the event: no dual write, no lost events.
/// </summary>
// insight - the course dispatched in SavedChangesAsync (after commit), so a crash between commit and publish lost the event
public sealed class DispatchDomainEventsInterceptor(IServiceProvider serviceProvider) : SaveChangesInterceptor
{
    private const int MaxRounds = 10;

    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> _dispatchers = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await DispatchPendingEventsAsync(eventData.Context, cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // insight - fail loudly instead of silently skipping events on the sync path
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null && AggregatesWithEvents(eventData.Context).Count > 0)
            throw new InvalidOperationException("Aggregates with pending domain events must be saved with SaveChangesAsync.");

        return base.SavingChanges(eventData, result);
    }

    private async Task DispatchPendingEventsAsync(DbContext context, CancellationToken ct)
    {
        // a handler may change another aggregate and raise more events: drain until quiet
        for (var round = 0; round < MaxRounds; round++)
        {
            var aggregates = AggregatesWithEvents(context);
            if (aggregates.Count == 0)
                return;

            var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();
            aggregates.ForEach(aggregate => aggregate.ClearDomainEvents());

            foreach (var domainEvent in domainEvents)
                await _dispatchers.GetOrAdd(domainEvent.GetType(), CreateDispatcher)(serviceProvider, domainEvent, ct);
        }

        throw new InvalidOperationException($"Domain events still pending after {MaxRounds} rounds; handlers are raising events in a cycle.");
    }

    private static List<IAggregateRoot> AggregatesWithEvents(DbContext context) =>
        context.ChangeTracker.Entries()
            .Select(entry => entry.Entity)
            .OfType<IAggregateRoot>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

    // insight - a typed delegate per event type (built once) instead of MethodInfo.Invoke,
    // so a handler's DomainException surfaces as-is rather than wrapped in TargetInvocationException
    private static Func<IServiceProvider, IDomainEvent, CancellationToken, Task> CreateDispatcher(Type eventType) =>
        typeof(DispatchDomainEventsInterceptor)
            .GetMethod(nameof(DispatchToHandlersAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(eventType)
            .CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>();

    private static async Task DispatchToHandlersAsync<TEvent>(IServiceProvider serviceProvider, IDomainEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
            await handler.HandleAsync((TEvent)domainEvent, ct);
    }
}
