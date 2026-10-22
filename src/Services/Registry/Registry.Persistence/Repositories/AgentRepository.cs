namespace Registry.Persistence.Repositories;

public sealed class AgentRepository(RegistryDbContext dbContext)
    : RepositoryBase<RegistryDbContext, Agent>(dbContext)
{
    public Task<bool> ClientIdExistsAsync(string clientId, CancellationToken ct = default) =>
        Entities.AnyAsync(agent => agent.ClientId == clientId, ct);
}
