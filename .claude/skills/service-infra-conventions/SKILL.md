---
name: service-infra-conventions
description: Cross-cutting wiring and repo conventions for AgentGate services - Aspire ServiceDefaults (AddServiceDefaults/MapDefaultEndpoints, OpenTelemetry trace context instead of correlation middleware), IClaimsProvider as the only HttpContext reader, fail-fast AddAndValidateOptions, the Mediator pipeline order (AssignActor -> Validation -> Logging), default interface methods, HTTP JSON settings, FluentValidation, central package management, GlobalUsings and naming. Use when wiring a service's infrastructure, binding options, touching the pipeline, or writing a new project file.
user-invocable: true
---

# Service Infra & Conventions

Two through-lines:
1. **Inner layers depend on a narrow capability, never on the transport** - no `HttpContext` below the API.
2. **Required config fails at startup**, not at first use.

## Cheat sheet

| Concern | Rule |
|---|---|
| Hosting | `builder.AddServiceDefaults()` + `app.MapDefaultEndpoints()` in every service |
| Correlation | W3C trace context via OpenTelemetry; no RequestContext/correlation middleware |
| Caller identity | inject `IClaimsProvider`; only `HttpContextClaimsProvider` touches `HttpContext` |
| Options | `AddAndValidateOptions<T>(configuration)` only |
| Pipeline | `AddMediator(o => o.ServiceLifetime = ServiceLifetime.Scoped)` + `AddPipelineBehaviors()` |
| Shared derivations | default interface methods, not base classes |
| JSON | framework camelCase default + `JsonStringEnumConverter` |
| Packages | `Directory.Packages.props`; no `Version=` in a csproj |

## 1. Aspire ServiceDefaults

`AgentGate.ServiceDefaults` gives every service OpenTelemetry logs/metrics/traces (ASP.NET Core,
HttpClient, runtime, MassTransit), health checks, service discovery and resilient `HttpClient`s.
`MapDefaultEndpoints()` maps `/health` and `/alive` in development only.

One trace runs agent -> Gateway -> MCP server and across RabbitMQ and gRPC, so there is **no**
`X-Correlation-ID` header, `RequestContext` or diagnostics middleware - use `Activity.Current` if you need
the trace id. Custom spans use an `ActivitySource` named `builder.Environment.ApplicationName` (already
registered).

## 2. Caller identity

```csharp
public interface IClaimsProvider            // Blocks.Core
{
    string? TryGetActorId();                // 'sub'
    string GetActorId() => TryGetActorId() ?? throw new UnauthorizedAccessException("Missing subject claim.");
    IReadOnlySet<string> GetRoles();
}
```

Registered by `AddApiDefaults()`. Never inject `IHttpContextAccessor` into a handler, behavior,
repository or domain method.

## 3. Options - fail fast

```csharp
builder.Services.AddAndValidateOptions<DriftDetectionOptions>(builder.Configuration);   // section "DriftDetectionOptions"

public sealed class DriftDetectionOptions
{
    [Range(5, 3600)] public int IntervalSeconds { get; init; }
}
```

Binds the section named after the type, validates DataAnnotations, `ValidateOnStart()`; a missing
section throws at boot. Never `services.Configure<T>(...)` for required config. Connection strings come
from Aspire (`GetConnectionString("{service}db")`, `"rabbitmq"`), not options.

## 4. Mediator pipeline

```csharp
builder.Services.AddMediator(o => o.ServiceLifetime = ServiceLifetime.Scoped);  // API project (source generator lives there)
builder.Services.AddPipelineBehaviors();                                         // Blocks.Mediator
```

Registration order is pipeline order: `AssignActorBehavior` (stamp `sub` onto empty `ActorId`) ->
`ValidationBehavior` (FluentValidation, throws `ValidationException`) -> `LoggingBehavior` (warns on
handlers slower than 1 s; traces carry timings). Scoped because handlers take the scoped DbContext.
Don't reorder; don't register behaviors elsewhere. Behaviors implement
`IPipelineBehavior<TMessage, TResponse>` with `ValueTask<TResponse> Handle(TMessage, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken)`.

## 5. Default interface methods

```csharp
public interface IAuditableAction<out TActionType> : IAuditableAction where TActionType : struct, Enum
{
    TActionType ActionType { get; }
    string IAuditableAction.Action => ActionType.ToString();   // derived, no base class
}
```

## 6. Validators

FluentValidation `AbstractValidator<T>`, co-located with the command, registered by
`AddValidatorsFromAssembly(...)`. Lengths use `MaxLength.C*`. No custom message helpers exist - use the
FluentValidation defaults.

## 7. HTTP JSON

```csharp
builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

Keep the camelCase web default (the React console expects it); never set `PropertyNamingPolicy`.

## 8. Projects and packages

- `Directory.Build.props`: `net10.0`, nullable, implicit usings, **warnings as errors**, central package
  management with transitive pinning. A csproj has no `PropertyGroup` for these and no `Version=`.
- New package: add `<PackageVersion>` to the root `Directory.Packages.props`, then a bare
  `<PackageReference Include="..." />`. No nested `Directory.Packages.props`.
- Pins with a reason stay pinned: MassTransit 8.5.x (v9 commercial), Mediator instead of MediatR
  (MediatR 13+ commercial). Licences matter here; check before adding a dependency.

## 9. GlobalUsings and naming

Per-project `GlobalUsings.cs`, curated, grouped by header comment: third-party, building blocks,
domain, own layer.

```csharp
// Third-party libraries
global using Microsoft.EntityFrameworkCore;
// Building blocks
global using Blocks.Core;
global using Blocks.EntityFrameworkCore;
// Domain
global using Registry.Domain.McpServers;
```

Naming: declared private fields `_camelCase` (including `static readonly`); primary-constructor
parameters plain `camelCase` (`HttpContextClaimsProvider(IHttpContextAccessor httpContextAccessor)`);
no abbreviations (`command`, not `cmd`; `cancellationToken`/`ct` as in the existing code).

## Architecture tests

`tests/AgentGate.ArchitectureTests` encodes the layering rules (domain kernel framework-free, exceptions
a leaf, Core free of infrastructure frameworks). Add a rule there when a convention can be checked
mechanically, instead of documenting it.
