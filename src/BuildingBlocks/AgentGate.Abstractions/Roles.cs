namespace AgentGate.Abstractions;

/// <summary>The closed role vocabulary. Each value equals a realm role in Realms/agentgate-realm.json.</summary>
public static class Roles
{
    /// <summary>Registers MCP servers and agents, and requests grants.</summary>
    public const string RegistryAdmin = "registry-admin";

    /// <summary>Reviews manifests and approves grants; every approval needs two different reviewers.</summary>
    public const string SecurityReviewer = "security-reviewer";

    /// <summary>Read-only access to inventory, reviews, grants and the audit trail.</summary>
    public const string Auditor = "auditor";

    /// <summary>Service-to-service: may call Registry's GetServerSnapshot (held by Review's client only).</summary>
    public const string SnapshotReader = "snapshot-reader";

    /// <summary>Service-to-service: may read the state the gateway enforces, to rebuild its copy after a restart.</summary>
    public const string GatewaySync = "gateway-sync";
}
