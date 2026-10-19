---
name: add-integration-event
description: Propagate a business fact across AgentGate service boundaries over RabbitMQ with MassTransit 8.5 - a flat record in AgentGate.IntegrationEvents, a PublishIntegrationEventOn{Event}Handler (IDomainEventHandler) that maps the aggregate carried by the domain event and calls IPublishEndpoint.Publish into the EF bus outbox (same commit as the change), and auto-discovered consumers. Use when Registry/Review/Gateway must tell another service something (ServerApproved, ServerQuarantined, GrantActivated, AnomalyDetected...). Not for in-process reactions or synchronous reads.
user-invocable: true
---

# Add Integration Event

A domain event stays in-process and carries the aggregate; an integration event leaves the process and
carries **only flat data**. This skill adds the second on top of an existing domain event.

```
Aggregate.AddDomainEvent(ServerQuarantined)          in-process, carries McpServer
  -> repository.SaveChangesAsync
     -> DispatchDomainEventsInterceptor (SavingChangesAsync, before commit)
        -> PublishIntegrationEventOnServerQuarantinedHandler
           -> IPublishEndpoint.Publish(...)        bus outbox: writes OutboxMessage rows to the same DbContext
     -> COMMIT (change + outbox rows together)
  -> MassTransit delivery service sends from the outbox -> RabbitMQ fan-out
     -> Gateway consumer, Review consumer, Hub consumer   (one queue per service per consumer)
```

Three edits: contract, publisher handler, one consumer per receiver. Messaging wiring
(`builder.AddMessaging<TDbContext>(serviceName, assembly)` in `Blocks.Messaging`) is already in every
stateful service - you never touch it.

## Phase 1 - contract (target shape: `src/BuildingBlocks/AgentGate.IntegrationEvents/`)

One folder per **publishing** service (`Registry/`, `Review/`, `Gateway/`), namespace
`AgentGate.IntegrationEvents.{Publisher}`. Grep for an existing event first.

```csharp
namespace AgentGate.IntegrationEvents.Registry;

public sealed record ServerQuarantined(Guid ServerId, string Reason, string ObservedHash);

public sealed record ServerApproved(Guid ServerId, string Url, string ManifestHash, IReadOnlyList<ToolDto> Tools);

public sealed record ToolDto(string Name, string? Title, string? Description, string InputSchema);
```

Rules:
- Records of primitives, strings, Guids, timestamps and nested DTO records only - never a domain entity,
  value object or domain enum (send status as its name).
- The project references nothing (no domain, no MassTransit, no EF).
- Names come from the plan's event table (`docs/plan.md` > Communication); no `Event` suffix.
- Change contracts additively; a consumer may still be on the old shape.

## Phase 2 - publisher handler (the only domain -> integration handoff)

Named after the **domain** event it handles, in the feature folder that raises it. It is a normal
`IDomainEventHandler`, so it runs inside the save.

```csharp
using Contracts = AgentGate.IntegrationEvents.Registry;

namespace Registry.Application.Features.McpServers.QuarantineServer;

public sealed class PublishIntegrationEventOnServerQuarantinedHandler(IPublishEndpoint publishEndpoint)
    : IDomainEventHandler<ServerQuarantined>
{
    public Task HandleAsync(ServerQuarantined domainEvent, CancellationToken ct) =>
        publishEndpoint.Publish(
            new Contracts.ServerQuarantined(domainEvent.Server.Id, domainEvent.Action.Comment ?? "", domainEvent.ObservedHash),
            ct);
}
```

- Map from the aggregate **on the event** - do not re-fetch it (a new aggregate isn't inserted yet).
- `IPublishEndpoint` from the request scope is the bus outbox: `Publish` only adds rows; the caller's
  `SaveChangesAsync` commits them. Never call `SaveChanges` here.
- Domain and contract records often share a name - alias the contract namespace as above.
- `_publishEndpoint.Publish` appears nowhere else in a stateful service.

Prerequisite: the DbContext maps `AddInboxStateEntity/AddOutboxMessageEntity/AddOutboxStateEntity`
(`persistence-patterns`).

**Gateway exception.** Gateway has no DbContext, so it uses `AddMessaging(serviceName, assembly)` without
an outbox and publishes directly (`ToolCallObserved`, `AnomalyDetected`). A lost message is acceptable for
an observation stream (ADR); do not add an outbox to Gateway.

## Phase 3 - consumers

One `IConsumer<TEvent>` per receiving service, in that service's Application project (the assembly passed
to `AddMessaging`). Discovered by `AddConsumers(assembly)`; endpoint names are
`{service}-{consumer}` kebab-case, so every service gets its own queue. No manual registration.
Idempotency, shadow data and the consumer body: `consumer-patterns`.

## Binding rules

1. Flat records, never entities.
2. One handoff seam: `PublishIntegrationEventOn{DomainEvent}Handler`.
3. Publish through the outbox (stateful services) - the change and the event commit together.
4. No manual consumer registration; never edit `Blocks.Messaging` to add an event.
5. MassTransit stays on 8.5.x (v9 is commercial); don't bump it.

## Verify

```bash
grep -rn "Domain" src/BuildingBlocks/AgentGate.IntegrationEvents --include=*.cs   # nothing
grep -rn "\.Publish(" src/Services --include=*.cs                                 # only PublishIntegrationEventOn* (+ Gateway)
grep -rn "AddConsumer\b" src/Services                                             # nothing
dotnet build
```

One integration test per new publisher: save once, assert one `OutboxMessage` row committed with the
change (and none when the save fails).
