namespace Review.Domain.Grants;

/// <summary>
/// Permission for one agent to call named tools on one MCP server, optionally until a fixed time.
/// Activation takes two different reviewers and a live check that the server is approved and still has the tools.
/// </summary>
public partial class Grant : AggregateRoot
{
    private Grant() { }

    public Guid AgentId { get; private init; }

    /// <summary>The agent's Keycloak client id: what the gateway matches against the token's <c>azp</c>.</summary>
    public string AgentClientId { get; private init; } = default!;

    public Guid ServerId { get; private init; }

    /// <summary>The server's route key at the gateway.</summary>
    public string ServerName { get; private init; } = default!;

    public IReadOnlyList<string> Tools { get; private init; } = [];
    public DateTime? ExpiresAt { get; private init; }
    public string Justification { get; private init; } = default!;

    public GrantStatus Status { get; private set; }
    public string? FirstApproverId { get; private set; }

    private readonly List<GrantAuditEntry> _auditEntries = [];
    public IReadOnlyList<GrantAuditEntry> AuditEntries => _auditEntries.AsReadOnly();
}
