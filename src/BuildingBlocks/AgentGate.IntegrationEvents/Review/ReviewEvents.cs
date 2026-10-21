namespace AgentGate.IntegrationEvents.Review;

// insight - status and risk travel as names, not enums: contracts reference nothing and consumers map what they know

/// <param name="Decision">"Approved" or "Rejected".</param>
/// <param name="RiskLevel">"Low", "Medium" or "High", from the risk scan.</param>
/// <param name="DecidedBy">The reviewers who made the decision, in order (two for an approval).</param>
public sealed record ManifestReviewCompleted(Guid ReviewId, Guid ServerId, string ManifestHash, string Decision, string RiskLevel,
    IReadOnlyList<string> DecidedBy, string? Comment, DateTime OccurredOn);

/// <summary>The grant is in force: published on activation and again on resume.</summary>
/// <param name="AgentClientId">The agent's Keycloak client id (the token's <c>azp</c> at the gateway).</param>
/// <param name="ServerName">The server's route key at the gateway (<c>/mcp/{ServerName}</c>).</param>
public sealed record GrantActivated(Guid GrantId, Guid AgentId, string AgentClientId, Guid ServerId, string ServerName,
    IReadOnlyList<string> Tools, DateTime? ExpiresAt, DateTime OccurredOn);

public sealed record GrantSuspended(Guid GrantId, string Reason, DateTime OccurredOn);

public sealed record GrantRevoked(Guid GrantId, string Reason, DateTime OccurredOn);
