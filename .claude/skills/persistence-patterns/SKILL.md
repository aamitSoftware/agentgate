---
name: persistence-patterns
description: EF Core on Postgres for AgentGate services - the {X}DbContext with MassTransit inbox/outbox table mappings, DbContext registration with the DispatchDomainEventsInterceptor, the EntityConfiguration/AuditedEntityConfiguration ladder (Guid v7 ids, ValueGeneratedNever, MaxLength constants), per-aggregate RepositoryBase subclasses as the unit of work, and JSON-seeded reference tables. Use when adding or changing a DbContext, entity configuration, repository, seed data or migration in a {X}.Persistence project.
---

# Persistence Patterns

Rules that make code wrong here, not just unidiomatic:

1. **Postgres only**, one database per service (`registrydb`, `reviewdb`, `hubdb`). No SQL Server, Redis,
   Mongo or Hasura. Gateway has no database.
2. **Ids are generated in code** (`Entity.Id = Guid.CreateVersion7()`), mapped `ValueGeneratedNever`.
3. **The repository is the unit of work** - `SaveChangesAsync` lives on it. No `IUnitOfWork`, no
   `IRepository` interface with one implementation.
4. **Audit is mandatory for aggregates** (`AuditedEntityConfiguration<T>`), set from the action in code -
   no database defaults.

## Phase 1 - DbContext

```csharp
// Registry.Persistence/RegistryDbContext.cs
public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options) : DbContext(options)
{
    public DbSet<McpServer> McpServers => Set<McpServer>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<ServerStatusTransition> ServerStatusTransitions => Set<ServerStatusTransition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RegistryDbContext).Assembly);

        // MassTransit EF inbox + bus outbox tables (required by AddMessaging<RegistryDbContext>)
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
```

Name it `{Service}DbContext`. Hub maps the three MassTransit tables too (its consumers use the inbox).

## Phase 2 - registration

```csharp
// Registry.Persistence/DependencyInjection.cs
public static IHostApplicationBuilder AddPersistenceServices(this IHostApplicationBuilder builder)
{
    builder.Services.AddDbContext<RegistryDbContext>((serviceProvider, options) => options
        .UseNpgsql(builder.Configuration.GetConnectionString("registrydb"))
        .AddInterceptors(serviceProvider.GetRequiredService<DispatchDomainEventsInterceptor>()));
    builder.EnrichNpgsqlDbContext<RegistryDbContext>();   // Aspire: tracing, health check, retries

    builder.Services.AddScoped<McpServerRepository>();
    builder.Services.AddScoped<AgentRepository>();
    return builder;
}
```

The interceptor itself is registered by `AddDomainEventDispatch` (`domain-event-wiring`). The
connection name matches the database added in the AppHost.

## Phase 3 - entity configurations

| Entity | Base | Gives you |
|---|---|---|
| Aggregate root | `AuditedEntityConfiguration<T>` | key, `ValueGeneratedNever`, `CreatedBy/On`, `LastModifiedBy/On`, ignores `DomainEvents` |
| Child entity | `EntityConfiguration<T>` | key, `ValueGeneratedNever` |
| Reference table without `Guid Id` (transitions) | `IEntityTypeConfiguration<T>` | you set the key |

```csharp
public sealed class McpServerConfiguration : AuditedEntityConfiguration<McpServer>
{
    public override void Configure(EntityTypeBuilder<McpServer> builder)
    {
        base.Configure(builder);                                  // always first
        builder.Property(s => s.Name).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.HasMany(s => s.AuditEntries).WithOne().HasForeignKey("McpServerId").OnDelete(DeleteBehavior.Cascade);
    }
}
```

- String lengths come from `MaxLength.C16 ... C2048` (Blocks.Core); `HasMaxLength(500)` is wrong.
- Enums are stored as strings.
- Value objects: `HasConversion(vo => vo.Value, value => Vo.Create(value))`, or `OwnsOne` for multi-field ones.

## Phase 4 - repositories

```csharp
// Registry.Persistence/Repositories/McpServerRepository.cs
public sealed class McpServerRepository(RegistryDbContext dbContext)
    : RepositoryBase<RegistryDbContext, McpServer>(dbContext)
{
    public override IQueryable<McpServer> Query() => Entities.Include(server => server.AuditEntries);

    public Task<McpServer?> GetByUrlAsync(string url, CancellationToken ct = default) =>
        Query().SingleOrDefaultAsync(server => server.Url == ServerUrl.Create(url), ct);
}
```

`RepositoryBase` gives `Query()`, `GetByIdAsync`, `GetByIdOrThrowAsync` (404), `ExistsAsync`,
`AddAsync`, `Remove`, `SaveChangesAsync`. Write paths load **tracked** (the default) so changes and
domain events are picked up; read-only query handlers use the DbContext with `AsNoTracking()` and a
projection (`create-feature-slice`). Register each repository with one `AddScoped` line.

## Phase 5 - seeded reference tables

Reference data (state-machine transitions) is a JSON file seeded through `HasData`, so it lands in a
migration:

```csharp
public sealed class ServerStatusTransitionConfiguration : IEntityTypeConfiguration<ServerStatusTransition>
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new JsonStringEnumConverter() } };

    public void Configure(EntityTypeBuilder<ServerStatusTransition> builder)
    {
        builder.HasKey(transition => new { transition.CurrentStatus, transition.ActionType });
        builder.Property(transition => transition.CurrentStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(transition => transition.ActionType).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(transition => transition.DestinationStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);

        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ServerStatusTransition.json");
        builder.HasData(JsonSerializer.Deserialize<List<ServerStatusTransition>>(File.ReadAllText(path), _json)!);
    }
}
```

The file must be copied to output, or `dotnet ef` fails to find it:

```xml
<None Update="Data\ServerStatusTransition.json" CopyToOutputDirectory="PreserveNewest" />
```

## Migrations

```bash
dotnet ef migrations add Name -p src/Services/{X}/{X}.Persistence -s src/Services/{X}/{X}.API
```

Adding a migration needs no database. The connection string only exists when Aspire runs the service;
how migrations get applied in dev is not decided yet - don't invent a startup migrator without asking.

## Not this skill

Domain modeling (`create-aggregate`), dispatch semantics (`domain-event-wiring`), the transition table's
runtime use (`governance-state-machine`).
