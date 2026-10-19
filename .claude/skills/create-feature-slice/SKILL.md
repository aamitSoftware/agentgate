---
name: create-feature-slice
description: Add a vertical-slice feature to an AgentGate service - the feature folder with a Mediator command or query record, co-located FluentValidation validator and ICommandHandler/IQueryHandler returning ValueTask, plus a thin Minimal API endpoint (bind, sender.Send(command with {...}), TypedResults) with a :verb route and a role policy. Use when adding any HTTP operation to Registry, Review, Gateway or Hub.
user-invocable: true
---

# Create Feature Slice

One feature = one folder. Minimal APIs everywhere; `Mediator` (martinothamar, source-generated) for
dispatch - **not** MediatR, no FastEndpoints, no Carter.

```
{X}.Application/Features/{Area}/{Operation}/   command/query + validator + handler (+ its domain-event handlers)
{X}.API/Endpoints/{Operation}Endpoint.cs        thin endpoint, one line in EndpointRegistration.MapAllEndpoints()
```

Handlers, validators and behaviors are discovered automatically (source generator / assembly scan) - no
DI edits. The pipeline is AssignActor -> Validation -> Logging (`Blocks.Mediator`).

## 1. Shared action base (once per aggregate, `Features/_Shared/`)

Write commands are the auditable action. Provenance is `[JsonIgnore]` and stamped by
`AssignActorBehavior` from the token's `sub` - never trusted from the body.

```csharp
public abstract record GrantCommand : IAuditableAction<GrantActionType>
{
    [JsonIgnore] public Guid GrantId { get; init; }                 // bound from the route
    [JsonIgnore] public string ActorId { get; set; } = "";          // stamped by the pipeline, or a system actor
    [JsonIgnore] public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string? Comment { get; init; }
    public abstract GrantActionType ActionType { get; }
}

public abstract class GrantCommandValidator<TCommand> : AbstractValidator<TCommand> where TCommand : GrantCommand
{
    protected GrantCommandValidator() => RuleFor(command => command.GrantId).NotEmpty();
}
```

The base does not implement `ICommand`; each concrete command adds the Mediator marker itself.

## 2. Command + validator + handler (`Features/Grants/SuspendGrant/`)

```csharp
public sealed record SuspendGrantCommand : GrantCommand, ICommand
{
    public override GrantActionType ActionType => GrantActionType.Suspend;
}

public sealed class SuspendGrantValidator : GrantCommandValidator<SuspendGrantCommand>
{
    public SuspendGrantValidator() => RuleFor(command => command.Comment).MaximumLength(MaxLength.C512);
}

public sealed class SuspendGrantHandler(GrantRepository grants, GrantStateMachineFactory stateMachineFactory)
    : ICommandHandler<SuspendGrantCommand>
{
    public async ValueTask<Unit> Handle(SuspendGrantCommand command, CancellationToken cancellationToken)
    {
        var grant = await grants.GetByIdOrThrowAsync(command.GrantId, cancellationToken);   // 404 if missing
        grant.Suspend(stateMachineFactory, command);                                         // domain rules + events
        await grants.SaveChangesAsync(cancellationToken);   // dispatches domain events + outbox in one commit
        return Unit.Value;
    }
}
```

Handler shape is always: load, call one existing domain behavior, save, return. No role checks, no
try/catch, no `Result<T>` - failures throw (`error-handling`).

A create returns the new id: `ICommand<RegisterServerResponse>` with
`public sealed record RegisterServerResponse(Guid Id);`.

## 3. Query

Reads skip the aggregate: no tracking, project straight to a DTO.

```csharp
public sealed record GetGrantQuery(Guid GrantId) : IQuery<GrantDto>;

public sealed class GetGrantHandler(ReviewDbContext db) : IQueryHandler<GetGrantQuery, GrantDto>
{
    public async ValueTask<GrantDto> Handle(GetGrantQuery query, CancellationToken cancellationToken) =>
        (await db.Grants.AsNoTracking()
            .Where(grant => grant.Id == query.GrantId)
            .Select(grant => new GrantDto(grant.Id, grant.AgentId, grant.ServerId, grant.Status.ToString()))
            .SingleOrDefaultAsync(cancellationToken))
        .OrThrowNotFound();
}
```

Mapster `Adapt<T>()` is allowed when member names line up; otherwise project or map by hand.

## 4. Endpoint (`{X}.API/Endpoints/`)

```csharp
public static class SuspendGrantEndpoint
{
    public static void Map(this IEndpointRouteBuilder app) =>
        app.MapPost("/grants/{grantId:guid}:suspend",
                async (Guid grantId, SuspendGrantCommand command, ISender sender, CancellationToken cancellationToken) =>
                {
                    await sender.Send(command with { GrantId = grantId }, cancellationToken);
                    return TypedResults.NoContent();
                })
            .RequireAuthorization(Policies.SecurityReviewer)
            .WithName("SuspendGrant")
            .WithTags("Grants");
}
```

Then one line in `EndpointRegistration.MapAllEndpoints()`: `SuspendGrantEndpoint.Map(app);`

Routes: REST resource first; non-CRUD actions get a `:verb` suffix (`/servers/{serverId:guid}:approve`,
`/grants/{grantId:guid}:suspend`). Plain CRUD stays verb-less (`POST /servers`, `GET /grants/{grantId:guid}`).
Create returns `TypedResults.Created($"/servers/{response.Id}", response)`; queries `TypedResults.Ok(dto)`.

## Binding rules

1. A feature is a folder; no `Controllers/`, `Validators/` or `Services/` god-folders.
2. Provenance (`ActorId`, `OccurredOn`, route ids) is `[JsonIgnore]`; only payload fields and `Comment`
   bind from the body.
3. Authorization is declarative on the endpoint; zero role logic in handlers or the domain.
4. Handlers return `ValueTask<T>` (`Unit` for no result) and only throw on failure.
5. Never re-register behaviors, handlers or validators by hand.

## Not this skill

Domain behavior methods (`create-aggregate`), events (`domain-event-wiring`, `add-integration-event`),
policies and roles (`authorization-security`).
