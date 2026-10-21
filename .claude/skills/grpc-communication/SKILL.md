---
name: grpc-communication
description: The one synchronous call in AgentGate - code-first protobuf-net.Grpc IRegistryService.GetServerSnapshot(serverId) returning status, manifest hash and tool names, served by Registry and called by Review only when approving a manifest and when activating a grant, to be authoritative at that transition. Use when touching that contract, its server or client, or when tempted to add another synchronous cross-service call (usually the answer is an event-built local copy instead).
user-invocable: true
---

# gRPC Communication

## Decide first: you almost certainly don't need gRPC

| Situation | Do this |
|---|---|
| A state transition must be true **right now** (manifest hash still current; server still approved) | `GetServerSnapshot` - the only contract |
| Any read path, any Hub need, any Gateway decision | local copy built from integration events |
| A field missing from the local copy | add it to the event, not a gRPC method |

Rules: exactly **one** contract; only **Review** calls it, only in **ApproveManifest** and
**ActivateGrant**; **Gateway never calls gRPC** (its hot path must not depend on Registry being up);
Hub registers no gRPC client.

## Phase 1 - contract (target shape: `src/BuildingBlocks/AgentGate.Grpc.Contracts/Registry/`)

```csharp
using System.ServiceModel;
using ProtoBuf;
using ProtoBuf.Grpc;

namespace AgentGate.Grpc.Contracts.Registry;

[Service("agentgate.registry.RegistryService")]       // protobuf-net.Grpc's own attributes: no System.ServiceModel
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

[ProtoContract]
public sealed class ServerSnapshot
{
    [ProtoMember(1)] public string Status { get; set; } = "";               // ServerStatus name; contracts don't reference domains
    [ProtoMember(2)] public string PinnedManifestHash { get; set; } = "";   // what agents see now
    [ProtoMember(3)] public string PendingManifestHash { get; set; } = "";  // what is under review now
    [ProtoMember(4)] public List<string> PinnedToolNames { get; set; } = [];
}
```

protobuf-net mechanics: members serialize by **number** - unique, start at 1, never renumber or reuse
once shipped; parameterless ctor; collections are `List<T>`; nested types need `[ProtoContract]`.

## Phase 2 - server (Registry.API)

```csharp
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

// DependencyInjection: builder.Services.AddCodeFirstGrpc();
// Program.cs:          app.MapGrpcService<RegistryGrpcService>().RequireAuthorization(Policies.SnapshotReader);
```

There is no gRPC exception interceptor: a `NotFoundException` reaches the client as an `RpcException`.
The caller treats any `RpcException` as "cannot verify" and refuses the transition - fail closed
(`RegistrySnapshots.GetSnapshotOrThrowAsync` turns it into a 503).

**Identity and transport (ADR 0004).** The caller authenticates as itself: Review's `agentgate-review`
client-credentials token, realm role `snapshot-reader`, never a user's token. gRPC needs HTTP/2 and a
cleartext Kestrel endpoint speaks one protocol, so under the AppHost Registry binds `GRPC_PORT` as
HTTP/2-only next to its REST `HTTP_PORT`.

## Phase 3 - client registration (Review.API)

```csharp
builder.Services.AddCodeFirstGrpcClient<IRegistryService>((serviceProvider, options) =>
        options.Address = new Uri("http://_grpc.registry"))   // the named HTTP/2 endpoint, via service discovery
    .AddHttpMessageHandler<ServiceTokenHandler>();           // Review's own token (Review.API/RegistryClient)
```

Never create a `GrpcChannel` by hand. The contract interface is then injectable.

## Phase 4 - handler usage: authoritative check, then mutate

Fetch the snapshot only for the approval that completes the decision (earlier approvals change nothing
agents see), pass the facts into the domain behavior, let the domain decide (`DomainException` -> 400).

```csharp
public sealed class ApproveReviewHandler(ManifestReviewRepository reviews, IRegistryService registry,
    ReviewStateMachineFactory stateMachineFactory) : ICommandHandler<ApproveReviewCommand>
{
    public async ValueTask<Unit> Handle(ApproveReviewCommand command, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdOrThrowAsync(command.ReviewId, cancellationToken);

        var currentPendingHash = review.AwaitsFinalApproval
            ? (await registry.GetSnapshotOrThrowAsync(review.ServerId, cancellationToken)).PendingManifestHash
            : null;

        review.Approve(currentPendingHash, stateMachineFactory, command);   // throws if the hash moved on
        await reviews.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
```

`ApproveGrantHandler` follows the same shape: on the activating approval it maps the snapshot to
`CurrentServerState`, and `grant.Approve` rejects unless the server is `Approved` and every granted tool
is in `PinnedToolNames`.

## The mistake to avoid

Calling Registry on a read path or from the Gateway. A service that must call upstream to answer is down
whenever upstream is. Replicate through events; keep gRPC for the two transition checks.
