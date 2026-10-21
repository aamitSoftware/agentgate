using MassTransit;

namespace Review.Persistence;

public sealed class ReviewDbContext(DbContextOptions<ReviewDbContext> options) : DbContext(options)
{
    public DbSet<ManifestReview> ManifestReviews => Set<ManifestReview>();
    public DbSet<Grant> Grants => Set<Grant>();
    public DbSet<KnownAgent> KnownAgents => Set<KnownAgent>();
    public DbSet<KnownServer> KnownServers => Set<KnownServer>();
    public DbSet<ReviewStatusTransition> ReviewStatusTransitions => Set<ReviewStatusTransition>();
    public DbSet<GrantStatusTransition> GrantStatusTransitions => Set<GrantStatusTransition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReviewDbContext).Assembly);

        // MassTransit EF inbox + bus outbox tables (required by AddMessaging<ReviewDbContext>)
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
