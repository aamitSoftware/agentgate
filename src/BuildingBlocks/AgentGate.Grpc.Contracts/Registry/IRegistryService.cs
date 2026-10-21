using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;

namespace AgentGate.Grpc.Contracts.Registry;

/// <summary>
/// Served by Registry, called only by Review at the two transitions that must be authoritative right now:
/// the final approval of a manifest review and the activation of a grant. Everything else is replicated by events.
/// </summary>
[Service("agentgate.registry.RegistryService")]
public interface IRegistryService
{
    [Operation("GetServerSnapshot")]
    ValueTask<ServerSnapshot> GetServerSnapshotAsync(ServerSnapshotRequest request, CallContext context = default);
}

[ProtoContract]
public sealed class ServerSnapshotRequest
{
    [ProtoMember(1)] public Guid ServerId { get; set; }
}

// members serialize by number: never renumber or reuse a number once shipped
[ProtoContract]
public sealed class ServerSnapshot
{
    /// <summary>The server's status name (contracts don't reference domain enums).</summary>
    [ProtoMember(1)] public string Status { get; set; } = "";

    /// <summary>What agents are shown now; empty until the first approval.</summary>
    [ProtoMember(2)] public string PinnedManifestHash { get; set; } = "";

    /// <summary>The manifest awaiting review; empty when nothing is pending.</summary>
    [ProtoMember(3)] public string PendingManifestHash { get; set; } = "";

    [ProtoMember(4)] public List<string> PinnedToolNames { get; set; } = [];
}
