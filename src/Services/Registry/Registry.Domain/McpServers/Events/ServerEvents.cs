namespace Registry.Domain.McpServers;

public sealed record ServerApproved(McpServer Server, IAuditableAction Action) : IDomainEvent;

public sealed record ServerQuarantined(McpServer Server, IAuditableAction Action) : IDomainEvent;

public sealed record ServerRejected(McpServer Server, string RejectedHash, IAuditableAction Action) : IDomainEvent;

/// <summary>
/// <see cref="McpServer.PendingManifest"/> needs a security review; <see cref="McpServer.PinnedManifest"/> is what it
/// would replace (null for a new server).
/// </summary>
public sealed record ManifestSubmitted(McpServer Server, IAuditableAction Action) : IDomainEvent;
