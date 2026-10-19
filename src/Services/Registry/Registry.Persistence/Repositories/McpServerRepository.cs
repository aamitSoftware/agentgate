namespace Registry.Persistence.Repositories;

public sealed class McpServerRepository(RegistryDbContext dbContext)
    : RepositoryBase<RegistryDbContext, McpServer>(dbContext)
{
    public override IQueryable<McpServer> Query() => Entities.Include(server => server.AuditEntries);

    public Task<bool> NameExistsAsync(string name, CancellationToken ct = default) =>
        Entities.AnyAsync(server => server.Name == name, ct);
}
