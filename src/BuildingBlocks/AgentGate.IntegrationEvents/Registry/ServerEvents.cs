namespace AgentGate.IntegrationEvents.Registry;

// insight - OccurredOn lets a consumer drop a stale event: approval and quarantine travel on separate queues,
// so delivery order between them is not guaranteed

/// <param name="Name">The server's route key at the gateway (<c>/mcp/{Name}</c>).</param>
/// <param name="ToolsJson">The pinned manifest: canonical JSON array of MCP Tool objects.</param>
public sealed record ServerApproved(Guid ServerId, string Name, string Url, string ManifestHash, string ToolsJson, DateTime OccurredOn);

public sealed record ServerQuarantined(Guid ServerId, string Name, string PinnedHash, string ObservedHash, string Reason, DateTime OccurredOn);
