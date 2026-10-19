---
name: create-aggregate
description: Create a DDD aggregate in an AgentGate service Domain layer (McpServer, Agent, Grant, ManifestReview or a new one) - the state/behavior partial split, code-generated Guid v7 ids, value objects with private ctor + static Create, framework-free domain events carrying the aggregate, child audit entries, and the matching Postgres EF configuration. Use when adding an aggregate or promoting an entity to own invariants. Not for HTTP features, event handlers or cross-service events.
user-invocable: true
---

# Create Aggregate

An aggregate is the write-side consistency boundary: it owns its children, enforces its invariants and is
the only thing that raises its domain events. Every aggregate is EF-backed on Postgres.

Neighbours: expose it over HTTP -> `create-feature-slice`; react to its events -> `domain-event-wiring`;
send them to other services -> `add-integration-event`; gate status changes -> `governance-state-machine`.

## Layout (one form for every service)

```
{X}.Domain/{Aggregate}s/{Aggregate}.cs              state partial
{X}.Domain/{Aggregate}s/Behaviors/{Aggregate}.cs    behavior partial (same class name, same namespace)
{X}.Domain/{Aggregate}s/Events/{Event}.cs           domain events
{X}.Domain/{Aggregate}s/ValueObjects/{Vo}.cs
{X}.Persistence/EntityConfigurations/{Aggregate}Configuration.cs
```

The Domain project references `Blocks.Domain` only (framework-free). It does **not** reference
`Blocks.Core`: Core depends on `Blocks.Exceptions`, and the domain must never see an HTTP type.

## Phase 1 - state partial: what it has

```csharp
namespace Registry.Domain.McpServers;

public partial class McpServer : AggregateRoot      // Blocks.Domain: Guid v7 Id + audit + domain events
{
    private McpServer() { }                         // EF; creation goes through the static factory

    public string Name { get; private init; } = default!;
    public ServerUrl Url { get; private init; } = default!;
    public ServerStatus Status { get; private set; }

    private readonly List<ServerAuditEntry> _auditEntries = [];
    public IReadOnlyList<ServerAuditEntry> AuditEntries => _auditEntries.AsReadOnly();
}
```

`Id` is generated in code (`Guid.CreateVersion7()` in `Entity`) - never by the database - so events and
outbox rows can reference it before `SaveChanges`. Children are private lists exposed read-only.

## Phase 2 - behavior partial: what it does

```csharp
namespace Registry.Domain.McpServers;

public partial class McpServer
{
    public static McpServer Discover(string name, ServerUrl url, IAuditableAction<ServerActionType> action)
    {
        var server = new McpServer
        {
            Name = name, Url = url, Status = ServerStatus.Discovered,
            CreatedBy = action.ActorId, CreatedOn = action.OccurredOn
        };
        server.AddDomainEvent(new ServerDiscovered(server, action));
        return server;
    }

    // parameter order: domain args -> domain services/delegates -> action LAST
    public void Quarantine(string observedHash, ServerStateMachineFactory stateMachineFactory,
        IAuditableAction<ServerActionType> action)
    {
        SetStatus(ServerStatus.Quarantined, stateMachineFactory, action);   // validates before mutating
        AddDomainEvent(new ServerQuarantined(this, observedHash, action));
    }
}
```

Rules for every behavior method:
- every state change takes an `IAuditableAction<TActionType>` (who, when, what, why) - the plan's
  `IGovernanceAction`. Commands implement it; system actors set an explicit `ActorId` such as
  `"system:drift-detector"`. Audit columns come from `action` (`MarkModified(action)` on updates).
- invariant broken -> `throw new DomainException(...)`; never an HTTP exception.
- status changes go through `SetStatus`, which runs the data-driven transition check first
  (`governance-state-machine`).
- `AddDomainEvent` is called only from inside the aggregate.

## Phase 3 - value objects

Wrap a primitive that has an invariant so an invalid value can't be constructed. Base classes from
`Blocks.Domain`: `StringValueObject`, `SingleValueObject<T>` (struct), `ValueObject` (multi-field).

```csharp
public sealed class ServerUrl : StringValueObject
{
    private ServerUrl(string value) => Value = value;

    public static ServerUrl Create(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
            throw new DomainException($"'{value}' is not an absolute URL.");
        return new ServerUrl(value);
    }
}
```

## Phase 4 - domain events

A record implementing `IDomainEvent` (Blocks.Domain, no framework marker) that carries the **full
aggregate** plus the action. Handlers read state from it instead of re-querying (rows aren't inserted yet
when they run).

```csharp
public sealed record ServerQuarantined(McpServer Server, string ObservedHash, IAuditableAction Action) : IDomainEvent;
```

## Phase 5 - EF configuration

```csharp
public sealed class McpServerConfiguration : AuditedEntityConfiguration<McpServer>
{
    public override void Configure(EntityTypeBuilder<McpServer> builder)
    {
        base.Configure(builder);   // key, ValueGeneratedNever, audit columns, ignores DomainEvents

        builder.Property(s => s.Name).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(s => s.Url).HasMaxLength(MaxLength.C2048).IsRequired()
            .HasConversion(url => url.Value, value => ServerUrl.Create(value));
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(MaxLength.C32);

        builder.HasMany(s => s.AuditEntries).WithOne()
            .HasForeignKey("McpServerId").OnDelete(DeleteBehavior.Cascade);
    }
}
```

Aggregates use `AuditedEntityConfiguration<T>`; child entities use `EntityConfiguration<T>`. Lengths
come from `MaxLength.C*`, never literals. Then add a migration (command in `create-service`).

## Repository

A per-aggregate subclass of `RepositoryBase<TContext, TEntity>`, overriding `Query()` when children must
load. Details in `persistence-patterns`.

## Verify

- `AddDomainEvent` appears only in the aggregate's own files.
- `grep -rn "Blocks.Exceptions\|HttpException\|Blocks.Core" src/Services/{X}/{X}.Domain` returns nothing.
- Each value object has a `private` ctor and a `static ... Create(`.
- Both files declare `public partial class {Aggregate}`; `dotnet build` passes; migration applies.
