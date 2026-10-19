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

[ServiceContract]
public interface IRegistryService
{
    [OperationContract]
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
    [ProtoMember(1)] public string Status { get; set; } = default!;        // ServerStatus name; contracts don't reference domains
    [ProtoMember(2)] public string ManifestHash { get; set; } = default!;
    [ProtoMember(3)] public List<string> ToolNames { get; set; } = [];
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
            ManifestHash = server.PinnedManifest?.Hash ?? "",
            ToolNames = server.PinnedManifest?.Tools.Select(tool => tool.Name).ToList() ?? []
        };
    }
}

// DependencyInjection: builder.Services.AddCodeFirstGrpc();
// Program.cs:          app.MapGrpcService<RegistryGrpcService>();
```

There is no gRPC exception interceptor: a `NotFoundException` reaches the client as an `RpcException`.
The caller treats any `RpcException` as "cannot verify" and refuses the transition - fail closed.
Service-to-service authentication for this call is not defined in the plan yet; decide it at M3 before
exposing the endpoint beyond the Aspire network.

## Phase 3 - client registration (Review.API)

```csharp
builder.Services.AddCodeFirstGrpcClient<IRegistryService>(options =>
    options.Address = new Uri("https+http://registry"));   // Aspire service discovery; resilience from ServiceDefaults
```

Never create a `GrpcChannel` by hand. The contract interface is then injectable.

## Phase 4 - handler usage: authoritative check, then mutate

Fetch the snapshot, pass the facts into the domain behavior, let the domain decide (`DomainException`
-> 400). Always pass the cancellation token through `CallOptions`.

```csharp
public sealed class ApproveManifestHandler(
    ManifestReviewRepository reviews, IRegistryService registry, ReviewStateMachineFactory stateMachineFactory)
    : ICommandHandler<ApproveManifestCommand>
{
    public async ValueTask<Unit> Handle(ApproveManifestCommand command, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdOrThrowAsync(command.ReviewId, cancellationToken);

        var snapshot = await registry.GetServerSnapshotAsync(
            new ServerSnapshotRequest { ServerId = review.ServerId },
            new CallOptions(cancellationToken: cancellationToken));

        review.Approve(snapshot.ManifestHash, stateMachineFactory, command);   // throws if the hash moved on
        await reviews.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
```

`ActivateGrant` follows the same shape: snapshot, then `grant.Activate(snapshot..., factory, command)`
rejects unless the server is `Approved` and every granted tool is in `ToolNames`.

## The mistake to avoid

Calling Registry on a read path or from the Gateway. A service that must call upstream to answer is down
whenever upstream is. Replicate through events; keep gRPC for the two transition checks.
