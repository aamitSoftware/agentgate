using AgentGate.Grpc.Contracts.Registry;
using ProtoBuf.Grpc;
using Registry.Persistence.Repositories;

namespace Registry.API.Grpc;

/// <summary>
/// The one synchronous read Registry serves: the server's state right now, for Review's authoritative checks.
/// An unknown server surfaces to the caller as an RpcException, which it treats as "cannot verify" (fail closed).
/// </summary>
public sealed class RegistryGrpcService(McpServerRepository servers) : IRegistryService
{
    public async ValueTask<ServerSnapshot> GetServerSnapshotAsync(ServerSnapshotRequest request, CallContext context = default)
    {
        var server = await servers.GetByIdOrThrowAsync(request.ServerId, context.CancellationToken);
        return new ServerSnapshot
        {
            Status = server.Status.ToString(),
            PinnedManifestHash = server.PinnedManifest?.Hash ?? "",
            PendingManifestHash = server.PendingManifest?.Hash ?? "",
            PinnedToolNames = server.PinnedManifest?.ToolNames.ToList() ?? []
        };
    }
}
