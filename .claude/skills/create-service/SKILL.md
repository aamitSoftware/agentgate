---
name: create-service
description: Scaffold a new AgentGate service skeleton under src/Services/{X}/ (Registry, Review, Gateway, Hub or a new one) - the {X}.API/.Application/.Domain/.Persistence projects, csproj references with central package management, per-layer DependencyInjection on IHostApplicationBuilder, Program.cs with ServiceDefaults, and the Aspire AppHost registration. Use when standing up a brand-new service, before any aggregate, feature or endpoint. Not for adding a feature to an existing service.
user-invocable: true
---

# Create Service

Produce an **empty, buildable** skeleton: structure and wiring only. Aggregates come from
`create-aggregate`, features from `create-feature-slice`, the DbContext body from `persistence-patterns`.

## Phase 0 - fix the shape

| Service | Layers | State | Messaging |
|---|---|---|---|
| Registry, Review | API, Application, Domain, Persistence | Postgres `{service}db`, aggregates | `AddMessaging<TDbContext>` (outbox + inbox) |
| Hub | API, Application, Domain (plain data bags), Persistence | Postgres projections, no aggregates | `AddMessaging<TDbContext>` (inbox) |
| Gateway | API, Application, Domain | local copy built from events, no DbContext | `AddMessaging` (no outbox) |

Create a layer only when it has something in it. Projects depend inward only:
`API -> Application -> Persistence -> Domain`. Never reference another service's projects; cross-service
contact goes through `AgentGate.IntegrationEvents` (async) or `AgentGate.Grpc.Contracts` (the one gRPC call).

## Phase 1 - folder tree

```
src/Services/{X}/
├─ {X}.API/            Endpoints/, EndpointRegistration.cs, DependencyInjection.cs, Program.cs, appsettings.json
├─ {X}.Application/    Features/{Area}/{Operation}/, StateMachines/, DependencyInjection.cs, GlobalUsings.cs
├─ {X}.Domain/         {Aggregate}s/ (state file, Behaviors/, Events/), GlobalUsings.cs
└─ {X}.Persistence/    EntityConfigurations/, Repositories/, Data/, {X}DbContext.cs, DependencyInjection.cs
```

## Phase 2 - csproj files

`Directory.Build.props` already sets `net10.0`, nullable, implicit usings, warnings-as-errors and central
package management, so a csproj has **no PropertyGroup and no `Version=`**. A package not yet in
`Directory.Packages.props` gets a `PackageVersion` line there in the same pass (today missing: the Npgsql EF
provider, `Stateless`, `Mapster`, `protobuf-net.Grpc*`, `Aspire.Keycloak.Authentication`,
`Microsoft.EntityFrameworkCore.Design`).

```xml
<!-- {X}.Domain.csproj: framework-free -->
<ItemGroup>
  <ProjectReference Include="..\..\..\BuildingBlocks\Blocks.Domain\Blocks.Domain.csproj" />
  <!-- target: AgentGate.Abstractions (roles, system actors) once it exists -->
</ItemGroup>

<!-- {X}.Persistence.csproj -->
<ItemGroup>
  <PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" />
  <PackageReference Include="MassTransit.EntityFrameworkCore" />   <!-- inbox/outbox table mappings -->
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\..\..\BuildingBlocks\Blocks.EntityFrameworkCore\Blocks.EntityFrameworkCore.csproj" />
  <ProjectReference Include="..\{X}.Domain\{X}.Domain.csproj" />
</ItemGroup>

<!-- {X}.Application.csproj -->
<ItemGroup>
  <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  <!-- <PackageReference Include="Stateless" />  only with a state machine -->
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\..\..\BuildingBlocks\Blocks.Mediator\Blocks.Mediator.csproj" />
  <ProjectReference Include="..\..\..\BuildingBlocks\Blocks.Messaging\Blocks.Messaging.csproj" />
  <ProjectReference Include="..\{X}.Persistence\{X}.Persistence.csproj" />
  <!-- target: AgentGate.IntegrationEvents / AgentGate.Grpc.Contracts when used -->
</ItemGroup>

<!-- {X}.API.csproj: Sdk="Microsoft.NET.Sdk.Web" -->
<ItemGroup>
  <PackageReference Include="Mediator.SourceGenerator">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Aspire.Keycloak.Authentication" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\..\..\AgentGate.ServiceDefaults\AgentGate.ServiceDefaults.csproj" />
  <ProjectReference Include="..\..\..\BuildingBlocks\Blocks.AspNetCore\Blocks.AspNetCore.csproj" />
  <ProjectReference Include="..\{X}.Application\{X}.Application.csproj" />
</ItemGroup>
```

Each project gets a curated `GlobalUsings.cs` (see `service-infra-conventions`).

## Phase 3 - DependencyInjection per layer

One `Add{Layer}Services(this IHostApplicationBuilder builder)` per layer (`AddMessaging` and the Aspire
Npgsql enrichment are `IHostApplicationBuilder` extensions, so all layers use it).

```csharp
// {X}.Application/DependencyInjection.cs
public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
{
    var assembly = typeof(DependencyInjection).Assembly;
    builder.Services
        .AddPipelineBehaviors()                 // Blocks.Mediator: AssignActor -> Validation -> Logging
        .AddValidatorsFromAssembly(assembly)
        .AddDomainEventDispatch(assembly);      // interceptor + every IDomainEventHandler<> here
    builder.AddMessaging<{X}DbContext>("{x}", assembly);   // consumers live here too
    return builder;
}

// {X}.API/DependencyInjection.cs
public static IHostApplicationBuilder AddApiServices(this IHostApplicationBuilder builder)
{
    // the generator lives in this project; handlers in referenced Application are discovered
    builder.Services.AddMediator(o => o.ServiceLifetime = ServiceLifetime.Scoped);
    builder.Services.AddApiDefaults();          // claims provider, ProblemDetails, GlobalExceptionHandler
    builder.Services.ConfigureHttpJsonOptions(json =>
        json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    // authentication + policies: see authorization-security
    // options: builder.Services.AddAndValidateOptions<SomeOptions>(builder.Configuration);
    return builder;
}
```

Persistence registration (DbContext + interceptor + repositories) is in `persistence-patterns`.

## Phase 4 - Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();          // OpenTelemetry, health checks, service discovery, resilient HttpClients
builder.AddPersistenceServices();
builder.AddApplicationServices();
builder.AddApiServices();

var app = builder.Build();

app.UseApiDefaults();                  // exception handler first, so it covers everything below
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();             // /health and /alive (development only)
app.MapAllEndpoints();

app.Run();
```

No request-context or correlation middleware: W3C trace context from OpenTelemetry carries the
correlation across HTTP, RabbitMQ and gRPC.

## Phase 5 - register with Aspire

Add the API project to `AgentGate.slnx` and reference it from `AgentGate.AppHost`. Aspire assigns ports and
service discovery resolves `https+http://{service}`, so there is no port convention. Target shape in
`AppHost.cs` (the existing resources need to be captured in variables first):

```csharp
var registryDb = postgres.AddDatabase("registrydb");

builder.AddProject<Projects.Registry_API>("registry")
    .WithReference(registryDb).WaitFor(registryDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(keycloak);
```

The connection names are contracts: `rabbitmq` is read by `Blocks.Messaging`, `{service}db` by the
Persistence DI.

## Verify

```bash
grep -rn 'Version=' src/Services/{X} --include=*.csproj          # expect nothing
grep -iE 'Persistence|Application|API' src/Services/{X}/{X}.Domain/{X}.Domain.csproj   # expect nothing
dotnet build && dotnet test tests/AgentGate.ArchitectureTests
```

## Not this skill

Business classes, contracts (integration events, gRPC), migrations (`dotnet ef migrations add Name
-p src/Services/{X}/{X}.Persistence -s src/Services/{X}/{X}.API`).
