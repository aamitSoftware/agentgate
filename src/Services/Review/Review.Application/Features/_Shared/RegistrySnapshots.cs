using AgentGate.Grpc.Contracts.Registry;
using Grpc.Core;
using ProtoBuf.Grpc;

namespace Review.Application.Features._Shared;

public static class RegistrySnapshots
{
    /// <summary>
    /// Asks Registry for the server's state right now. Any failure means the decision can't be verified, so nothing
    /// changes (fail closed) and the caller gets a 503 to retry.
    /// </summary>
    public static async Task<ServerSnapshot> GetSnapshotOrThrowAsync(this IRegistryService registry, Guid serverId, CancellationToken ct)
    {
        try
        {
            return await registry.GetServerSnapshotAsync(new ServerSnapshotRequest { ServerId = serverId }, new CallContext(new CallOptions(cancellationToken: ct)));
        }
        catch (RpcException exception) when (!ct.IsCancellationRequested)
        {
            throw new ServiceUnavailableException(
                $"Could not verify the server's current state with Registry ({exception.StatusCode}); nothing was changed.", exception);
        }
    }
}
