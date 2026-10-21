---
name: governance-state-machine
description: Data-driven status-transition validation for AgentGate aggregates - McpServer (Discovered -> UnderReview -> Approved -> Quarantined -> UnderReview -> Retired) and Grant (Requested -> PendingApproval -> Active <-> Suspended -> Revoked, plus Rejected). Covers the JSON-seeded {Aggregate}StatusTransition table, the Stateless wrapper, the factory delegate the domain receives, ValidateTransition before mutation, the audit entry, and IAuditableAction<TActionType> actions including system actors. Use when adding a status, action or transition, or giving a new aggregate a lifecycle.
---

# Governance State Machine

Legal moves are **data**: rows in a seeded `{Aggregate}StatusTransition` table, loaded once into a
Stateless machine, checked by `ValidateTransition` **before** the aggregate mutates `Status`. Adding a
legal move is a JSON edit plus a migration, never a `switch`.

Every change is carried by an action - `IAuditableAction<TActionType>` (Blocks.Domain): `ActorId`,
`OccurredOn`, `ActionType`, `Comment` (the plan's `IGovernanceAction`). The same object validates the
transition, writes the audit entry and rides on the domain event. Commands implement it
(`create-feature-slice`); system actors set an explicit id (`"system:drift-detector"`,
`"system:anomaly-detector"`), never null.

## Lifecycles (seed from the plan; adjust only with a plan change)

Rows match the seeds as of M3: `Registry.Persistence/Data/ServerStatusTransition.json`,
`Review.Persistence/Data/ReviewStatusTransition.json` and `GrantStatusTransition.json`. Discovered and Retire
for servers are still deferred.

| Aggregate (service) | CurrentStatus | ActionType | DestinationStatus |
|---|---|---|---|
| McpServer (Registry) | UnderReview (initial, set by `Register`) | Approve | Approved |
| | UnderReview, Quarantined | Reject | Rejected |
| | Approved | Quarantine | Quarantined |
| | Quarantined | Approve | Approved |
| ManifestReview (Review) | Open (initial, set by `Open`) | Approve | PartiallyApproved |
| | PartiallyApproved | Approve | Approved |
| | Open, PartiallyApproved | Reject | Rejected |
| | Open, PartiallyApproved | Supersede | Superseded |
| Grant (Review) | Requested | Approve | PendingApproval |
| | PendingApproval | Approve | Active |
| | Requested, PendingApproval | Reject | Rejected |
| | Active | Suspend | Suspended |
| | Suspended | Resume | Active |
| | Active, Suspended | Revoke | Revoked |

No `Expired` grant state: the Gateway checks `ExpiresAt` itself. The initial status is set by the
aggregate's factory (`Discover`, `Request`), not by a transition row.

## Phase 1 - transition entity + seed (Domain + Persistence)

```csharp
// Registry.Domain/McpServers/ServerStatusTransition.cs
public sealed class ServerStatusTransition
{
    public ServerStatus CurrentStatus { get; init; }
    public ServerActionType ActionType { get; init; }
    public ServerStatus DestinationStatus { get; init; }
}
```

```json
// Registry.Persistence/Data/ServerStatusTransition.json
[
  { "CurrentStatus": "Discovered",  "ActionType": "SubmitForReview", "DestinationStatus": "UnderReview" },
  { "CurrentStatus": "UnderReview", "ActionType": "Approve",         "DestinationStatus": "Approved" },
  { "CurrentStatus": "Approved",    "ActionType": "Quarantine",      "DestinationStatus": "Quarantined" }
]
```

Key is `(CurrentStatus, ActionType)` - one destination per action per state, which is also what Stateless
requires. Configuration, `HasData` from the JSON and the copy-to-output line: `persistence-patterns`
Phase 5.

## Phase 2 - seam in the Domain

```csharp
// Registry.Domain/McpServers/ServerStateMachine.cs
public interface IServerStateMachine
{
    bool CanFire(ServerActionType actionType);
}

public delegate IServerStateMachine ServerStateMachineFactory(ServerStatus status);

public static class ServerStateMachineFactoryExtensions
{
    public static void ValidateTransition(this ServerStateMachineFactory factory, ServerStatus status, ServerActionType actionType)
    {
        if (!factory(status).CanFire(actionType))
            throw new DomainException($"Action {actionType} is not allowed while the server is {status}.");
    }
}
```

The domain only sees the delegate; it never constructs the machine and never references Stateless.

## Phase 3 - Stateless wrapper + registration (Application)

```csharp
// Registry.Application/StateMachines/ServerStateMachine.cs
public sealed class ServerStateMachine : IServerStateMachine
{
    private readonly StateMachine<ServerStatus, ServerActionType> _machine;

    public ServerStateMachine(ServerStatus status, IReadOnlyList<ServerStatusTransition> transitions)
    {
        _machine = new StateMachine<ServerStatus, ServerActionType>(status);
        foreach (var transition in transitions)
            _machine.Configure(transition.CurrentStatus).Permit(transition.ActionType, transition.DestinationStatus);
    }

    public bool CanFire(ServerActionType actionType) => _machine.CanFire(actionType);
}

// Registry.Application/DependencyInjection.cs
builder.Services.AddSingleton<ServerStateMachineFactory>(serviceProvider =>
{
    using var scope = serviceProvider.CreateScope();
    // loaded once per process on first resolve; a reseeded table needs a restart
    var transitions = scope.ServiceProvider.GetRequiredService<RegistryDbContext>()
        .ServerStatusTransitions.AsNoTracking().ToList();
    return status => new ServerStateMachine(status, transitions);
});
```

Handlers and consumers take `ServerStateMachineFactory` as a dependency and pass it to the behavior.

## Phase 4 - the aggregate: validate, mutate, audit, raise

```csharp
// Registry.Domain/McpServers/Behaviors/McpServer.cs
private void SetStatus(ServerStatus newStatus, ServerStateMachineFactory stateMachineFactory,
    IAuditableAction<ServerActionType> action)
{
    stateMachineFactory.ValidateTransition(Status, action.ActionType);   // FIRST - before any mutation

    var previousStatus = Status;
    Status = newStatus;
    MarkModified(action);                                                // LastModifiedBy/On from the action
    _auditEntries.Add(ServerAuditEntry.Create(previousStatus, newStatus, action));
}

public void Quarantine(string observedHash, ServerStateMachineFactory stateMachineFactory,
    IAuditableAction<ServerActionType> action)
{
    SetStatus(ServerStatus.Quarantined, stateMachineFactory, action);
    AddDomainEvent(new ServerQuarantined(this, observedHash, action));
}
```

`ServerAuditEntry` (target shape) is a child `Entity` holding `Action`, `ActorId`, `OccurredOn`,
`Comment`, `FromStatus`, `ToStatus`. Public behaviors raise their specific domain event; `SetStatus`
stays private.

Rules the table can't express stay in the behavior, checked before `SetStatus` mutates anything - e.g.
four-eyes on `Grant.Approve` and `ManifestReview.Approve` (`Review.Domain/FourEyes.cs`): the completing
approval's `ActorId` must differ from the first approver, else `DomainException`. A behavior that adds such
rules calls `stateMachineFactory.ValidateTransition` first, so an illegal move reports as illegal rather
than as a broken rule. The authoritative manifest-hash check is passed in as a value
from the handler (`grpc-communication`).

`Grant` mirrors all of the above with `GrantStatus`, `GrantActionType`, `GrantStatusTransition`,
`IGrantStateMachine`, `GrantStateMachineFactory` in Review.

## Adding a transition

1. Row in `Data/{Aggregate}StatusTransition.json`; new enum member if the status/action is new.
2. `dotnet ef migrations add` (the seed lives in the migration); restart picks it up.
3. Domain unit test: the new move passes, an illegal one throws `DomainException` (-> 400).
