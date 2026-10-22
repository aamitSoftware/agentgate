using MassTransit;

namespace Hub.Persistence;

/// <summary>Projections only. Nothing here is written except by a consumer of another service's event.</summary>
public sealed class HubDbContext(DbContextOptions<HubDbContext> options) : DbContext(options)
{
    public DbSet<ServerView> Servers => Set<ServerView>();
    public DbSet<AgentView> Agents => Set<AgentView>();
    public DbSet<GrantView> Grants => Set<GrantView>();
    public DbSet<TimelineEntry> Timeline => Set<TimelineEntry>();
    public DbSet<ToolCallView> ToolCalls => Set<ToolCallView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HubDbContext).Assembly);

        // MassTransit EF inbox (consumers) + outbox tables (required by AddMessaging<HubDbContext>)
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
