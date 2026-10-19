---
name: consumer-patterns
description: Write MassTransit 8.5 IConsumer<T> classes for AgentGate integration events - the consumer shape, the single idempotency policy (MassTransit EF inbox via AddMessaging<TDbContext>), keeping a local copy of foreign data from the event payload, and the split between a write-side domain mutation (Registry/Review), a read-model projection (Hub) and the Gateway's in-memory local copy. Use when adding or editing a consumer of anything under AgentGate.IntegrationEvents.
user-invocable: true
---

# Consumer Patterns

A consumer is how one service reacts to another's fact. Shape, in order:

1. **Class + primary-constructor dependencies.** `IConsumer<TEvent>` in `{X}.Application/Features/{Area}/{Operation}/`,
   named `{Event}Consumer`. Persistence collaborators are concrete (`{X}DbContext`, `{Aggregate}Repository`);
   delegates like `GrantStateMachineFactory` are injected as-is.
2. **Local-copy hydration** from the event payload.
3. **Domain mutation or projection write.**
4. **One `SaveChangesAsync(context.CancellationToken)`** at the end.

## Idempotency: one policy - the EF inbox

`builder.AddMessaging<TDbContext>(serviceName, assembly)` puts the MassTransit EF **inbox** on every
endpoint: each consumed `MessageId` is recorded in the same transaction as the consumer's changes, so a
redelivery is a no-op. **Do not hand-roll duplicate checks** (no exists-then-return, no
throw-if-exists for idempotency).

What remains is ordinary domain behavior: an illegal transition still throws `DomainException`. Retries
(`100ms, 500ms, 1s, 5s`) skip `DomainException` - a broken rule won't fix itself - so the message goes
straight to the `_error` queue. Any other exception (e.g. a row from an earlier event not projected yet)
is retried.

**Gateway is the exception**: no DbContext, so no inbox. Its consumers must be idempotent by
construction - overwrite the local copy with the event's state (pin, block, remove), never increment.

## Local copy, not callbacks

Foreign data a service needs later is stored from the event payload into its own tables (Hub, Review) or
memory (Gateway). Never call the owning service back - the one sanctioned synchronous call is
`GetServerSnapshot` (`grpc-communication`). If a consumer needs a field the event lacks, add it to the
event (additively).

## Write-side consumer (Registry, Review): mutate through the aggregate (target shape)

The command record is the action. A system actor sets `ActorId` explicitly.

```csharp
// Review.Application/Features/Grants/SuspendOnAnomaly/AnomalyDetectedConsumer.cs
public sealed class AnomalyDetectedConsumer(GrantRepository grants, GrantStateMachineFactory stateMachineFactory)
    : IConsumer<AnomalyDetected>
{
    public async Task Consume(ConsumeContext<AnomalyDetected> context)
    {
        var anomaly = context.Message;
        var activeGrants = await grants.Query()
            .Where(grant => grant.AgentId == anomaly.AgentId && grant.Status == GrantStatus.Active)
            .ToListAsync(context.CancellationToken);

        foreach (var grant in activeGrants)
            grant.Suspend(stateMachineFactory, new SuspendGrantCommand
            {
                GrantId = grant.Id,
                ActorId = SystemActors.AnomalyDetector,   // "system:anomaly-detector" (target: AgentGate.Abstractions)
                Comment = anomaly.Reason
            });

        await grants.SaveChangesAsync(context.CancellationToken);   // domain events -> consumer outbox, same commit
    }
}
```

Never construct an aggregate's state directly in a consumer; go through its factory/behavior so
invariants and the transition table run.

## Projection consumer (Hub): plain data bags (target shape)

Hub entities are public get/set with no behavior; set fields or add rows directly.

```csharp
// Hub.Application/Features/Servers/Projections/ServerQuarantinedConsumer.cs
public sealed class ServerQuarantinedConsumer(HubDbContext db) : IConsumer<ServerQuarantined>
{
    public async Task Consume(ConsumeContext<ServerQuarantined> context)
    {
        var message = context.Message;
        var server = (await db.Servers.SingleOrDefaultAsync(row => row.Id == message.ServerId, context.CancellationToken))
            .OrThrowNotFound();                    // retried if ServerApproved hasn't been projected yet

        server.Status = "Quarantined";
        db.TimelineEntries.Add(new TimelineEntry
        {
            ServerId = message.ServerId, Kind = nameof(ServerQuarantined),
            Detail = message.Reason, OccurredOn = context.SentTime ?? DateTime.UtcNow
        });

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
```

A first-seen fact (e.g. `ServerApproved` for a new server) uses get-or-create on the local row:
look it up, add it if missing, then set fields from the event.

## Gateway local copy (target shape)

```csharp
public sealed class ServerApprovedConsumer(ApprovedServers servers) : IConsumer<ServerApproved>
{
    public Task Consume(ConsumeContext<ServerApproved> context)
    {
        servers.Pin(context.Message);   // overwrite by ServerId: a redelivery converges to the same state
        return Task.CompletedTask;
    }
}
```

## Verify

- No `ExistsAsync`/`AnyAsync` duplicate guards in consumers of stateful services.
- Consumers sit in the assembly passed to `AddMessaging`; nothing calls `AddConsumer` by hand.
- Integration test (MassTransit test harness): the same message delivered twice changes state once.

## Not this skill

Publishing (`add-integration-event`), in-process handlers (`domain-event-wiring`).
