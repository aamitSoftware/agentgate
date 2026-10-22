namespace Review.Domain.LocalCopies;

/// <summary>Registry's agent, as Review learned it from AgentRegistered. Read-only here; Registry owns it.</summary>
public sealed class KnownAgent : Entity
{
    private KnownAgent() { }

    public string Name { get; private set; } = default!;
    public string ClientId { get; private set; } = default!;
    public string Owner { get; private set; } = default!;
    public string Purpose { get; private set; } = default!;

    public static KnownAgent Create(Guid agentId, string name, string clientId, string owner, string purpose) =>
        new() { Id = agentId, Name = name, ClientId = clientId, Owner = owner, Purpose = purpose };
}
