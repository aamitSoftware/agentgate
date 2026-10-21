namespace Registry.Domain.Agents;

/// <summary>
/// A nonhuman identity: an AI agent that calls MCP tools through the gateway. Its Keycloak client is
/// pre-created in the realm; registering it here gives it an owner and a purpose, and makes it grantable.
/// </summary>
public partial class Agent : AggregateRoot
{
    private Agent() { }

    public string Name { get; private init; } = default!;

    /// <summary>The Keycloak client id the agent authenticates with: the token's <c>azp</c> at the gateway.</summary>
    public string ClientId { get; private init; } = default!;

    /// <summary>The accountable human or team.</summary>
    public string Owner { get; private init; } = default!;

    /// <summary>What the agent is for; reviewers weigh grant requests against it.</summary>
    public string Purpose { get; private init; } = default!;
}
