namespace Registry.Domain.McpServers;

public partial class McpServer : AggregateRoot
{
    private McpServer() { }

    /// <summary>Unique slug; also the gateway route key (<c>/mcp/{name}</c>).</summary>
    public string Name { get; private init; } = default!;
    public ServerUrl Url { get; private init; } = default!;
    public ServerStatus Status { get; private set; }

    /// <summary>What agents are shown and what drift is measured against. Null until first approval.</summary>
    public ToolManifest? PinnedManifest { get; private set; }

    /// <summary>The manifest awaiting review: submitted at registration, or observed on drift.</summary>
    public ToolManifest? PendingManifest { get; private set; }

    private readonly List<ServerAuditEntry> _auditEntries = [];
    public IReadOnlyList<ServerAuditEntry> AuditEntries => _auditEntries.AsReadOnly();
}
