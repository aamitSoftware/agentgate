namespace AgentGate.IntegrationEvents.Registry;

// insight - OccurredOn lets a consumer drop a stale event: approval and quarantine travel on separate queues,
// so delivery order between them is not guaranteed

/// <param name="Name">The server's route key at the gateway (<c>/mcp/{Name}</c>).</param>
/// <param name="ToolsJson">The pinned manifest: canonical JSON array of MCP Tool objects.</param>
public sealed record ServerApproved(Guid ServerId, string Name, string Url, string ManifestHash, string ToolsJson, DateTime OccurredOn);

public sealed record ServerQuarantined(Guid ServerId, string Name, string PinnedHash, string ObservedHash, string Reason, DateTime OccurredOn);

/// <summary>A manifest needs a security review: a new server was registered, or an approved one drifted.</summary>
/// <param name="ToolsJson">The manifest to review: canonical JSON array of MCP Tool objects.</param>
/// <param name="PreviousHash">The pinned manifest it would replace; null for a new server.</param>
/// <param name="PreviousToolsJson">The pinned manifest's tools, so the review can show what changed; null for a new server.</param>
public sealed record ManifestSubmitted(Guid ServerId, string Name, string ManifestHash, string ToolsJson,
    string? PreviousHash, string? PreviousToolsJson, string Reason, DateTime OccurredOn);
