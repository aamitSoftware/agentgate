using MassTransit;

namespace Registry.Persistence;

public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options) : DbContext(options)
{
    public DbSet<McpServer> McpServers => Set<McpServer>();
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
