---
name: domain-event-wiring
description: Write an in-process domain-event handler in an AgentGate service ({Effect}On{Event}Handler implementing IDomainEventHandler<TEvent> from Blocks.Domain) and wire the dispatch it needs - AddDomainEventDispatch(assembly) plus the DispatchDomainEventsInterceptor on the DbContext, which dispatches inside SavingChangesAsync before the commit. Use when adding a domain event reaction or wiring dispatch in a new service. NOT for publishing to other services (add-integration-event) or synchronous cross-service reads (grpc-communication).
user-invocable: true
---

# Domain Event Wiring

Domain events are the aggregate-owned, in-process side channel. They never leave the service; only a
flat projection does, through a `PublishIntegrationEventOn{Event}Handler` (`add-integration-event`).

There is one mechanism - no publisher interface, no library choice, no interceptor flavors.

## The model (Blocks.Domain, framework-free)

```csharp
public interface IDomainEvent;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
```

The aggregate raises events with `AddDomainEvent(...)` from its own behavior methods; the event record
carries the full aggregate plus the action (`create-aggregate`, Phase 4).

## How dispatch works

`DispatchDomainEventsInterceptor` (Blocks.EntityFrameworkCore) runs in **`SavingChangesAsync`, before the
commit**:

1. collects events from tracked `IAggregateRoot`s, clears them, calls every registered handler;
2. repeats while handlers raise new events (max 10 rounds; a cycle throws);
3. EF then saves everything the handlers added to the same DbContext - including MassTransit outbox
   rows - in **the same transaction** as the change. No dual write, no lost events
   (proved by `tests/Blocks.EntityFrameworkCore.Tests/DispatchDomainEventsInterceptorTests.cs`).

A handler exception aborts the save: the change and the handler's writes roll back together.
Sync `SaveChanges()` with pending events throws - always use `SaveChangesAsync`.

## Wiring (once per service with aggregates)

```csharp
// {X}.Application/DependencyInjection.cs - registers the interceptor + every handler in the assembly
builder.Services.AddDomainEventDispatch(typeof(DependencyInjection).Assembly);

// {X}.Persistence/DependencyInjection.cs - the interceptor MUST be added to the DbContext options
builder.Services.AddDbContext<RegistryDbContext>((serviceProvider, options) => options
    .UseNpgsql(builder.Configuration.GetConnectionString("registrydb"))
    .AddInterceptors(serviceProvider.GetRequiredService<DispatchDomainEventsInterceptor>()));
```

Forget `AddInterceptors` and events are silently never dispatched. Gateway (no DbContext) and Hub
(no aggregates) have no domain events.

## Writing a handler

Name it `{Effect}On{Event}Handler`, put it in the feature folder that raises the event. No DI line - the
assembly scan registers it (scoped, so it can take the scoped DbContext/repositories).

```csharp
// {X}.Application/Features/{Area}/{Operation}/{Effect}On{Event}Handler.cs
public sealed class {Effect}OnServerQuarantinedHandler(RegistryDbContext db)
    : IDomainEventHandler<ServerQuarantined>
{
    public Task HandleAsync(ServerQuarantined domainEvent, CancellationToken ct)
    {
        var server = domainEvent.Server;            // read from the event - do NOT re-query
        db.Add(/* an entity built from server */);
        return Task.CompletedTask;                  // do NOT call SaveChanges - the outer save commits it
    }
}
```

Handler rules:
- **No `SaveChanges`.** You are already inside one.
- **No re-query of the aggregate.** A newly added aggregate isn't inserted yet, so a query can't see it;
  read everything from the aggregate carried by the event.
- **Add or modify tracked entities freely** - they commit with the triggering change. Changing another
  aggregate that raises events is fine; those are dispatched in the next round.
- **Throw `DomainException`** to veto the whole save; it surfaces as-is (400 at the edge).
- No I/O that can't roll back (HTTP calls, email) - that would happen even if the commit fails.
  Cross-service effects go through the outbox (`add-integration-event`).

## Steps

1. Event record implementing `IDomainEvent` in `{Aggregate}s/Events/`.
2. `AddDomainEvent(...)` inside the behavior method.
3. Confirm `AddDomainEventDispatch` and `AddInterceptors(...)` are wired (new service only).
4. Handler in the feature folder, named by effect.
5. `dotnet build`; for a new wiring, an integration test that saves once and asserts the handler's row exists.

## Not this skill

Publishing integration events (`add-integration-event`); editing `Blocks.*` (stable infrastructure).
