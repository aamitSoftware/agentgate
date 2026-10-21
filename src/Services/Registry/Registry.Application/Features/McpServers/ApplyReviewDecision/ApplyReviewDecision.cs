using AgentGate.IntegrationEvents.Review;
using Contracts = AgentGate.IntegrationEvents.Registry;

namespace Registry.Application.Features.McpServers.ApplyReviewDecision;

/// <summary>The action that pins a reviewed manifest. Raised only by a completed review in the Review service.</summary>
public sealed record ApproveServerCommand : ServerCommand
{
    public override ServerActionType ActionType => ServerActionType.Approve;
}

public sealed record RejectServerCommand : ServerCommand
{
    public override ServerActionType ActionType => ServerActionType.Reject;
}

/// <summary>
/// Review owns the decision (risk scan + four-eyes); Registry owns the server. A completed review moves the server
/// through the same audited, state-checked behaviors as any other change.
/// </summary>
public sealed class ManifestReviewCompletedConsumer(McpServerRepository servers, ServerStateMachineFactory stateMachineFactory)
    : IConsumer<ManifestReviewCompleted>
{
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public async Task Consume(ConsumeContext<ManifestReviewCompleted> context)
    {
        var review = context.Message;
        var server = await servers.GetByIdOrThrowAsync(review.ServerId, context.CancellationToken);

        // the audit trail names the human who completed the decision; the comment names everyone involved
        var actorId = review.DecidedBy.LastOrDefault() ?? throw new DomainException("A review decision must name its reviewers.");
        var comment = $"{review.Decision} by {string.Join(", ", review.DecidedBy)} (risk {review.RiskLevel})" +
                      (string.IsNullOrWhiteSpace(review.Comment) ? "." : $": {review.Comment}");

        switch (review.Decision)
        {
            case Approved:
                server.Approve(review.ManifestHash, stateMachineFactory,
                    new ApproveServerCommand { ServerId = server.Id, ActorId = actorId, Comment = comment });
                break;
            case Rejected:
                server.Reject(review.ManifestHash, stateMachineFactory,
                    new RejectServerCommand { ServerId = server.Id, ActorId = actorId, Comment = comment });
                break;
            default:
                throw new DomainException($"Unknown review decision '{review.Decision}'.");
        }

        await servers.SaveChangesAsync(context.CancellationToken);
    }
}

public sealed class PublishIntegrationEventOnServerApprovedHandler(IPublishEndpoint publishEndpoint)
    : IDomainEventHandler<ServerApproved>
{
    public Task HandleAsync(ServerApproved domainEvent, CancellationToken ct)
    {
        var server = domainEvent.Server;
        var manifest = server.PinnedManifest!;
        return publishEndpoint.Publish(
            new Contracts.ServerApproved(server.Id, server.Name, server.Url.Value, manifest.Hash, manifest.ToolsJson,
                domainEvent.Action.OccurredOn),
            ct);
    }
}

public sealed class PublishIntegrationEventOnServerRejectedHandler(IPublishEndpoint publishEndpoint)
    : IDomainEventHandler<ServerRejected>
{
    public Task HandleAsync(ServerRejected domainEvent, CancellationToken ct) =>
        publishEndpoint.Publish(
            new Contracts.ServerRejected(domainEvent.Server.Id, domainEvent.Server.Name, domainEvent.RejectedHash,
                domainEvent.Action.Comment ?? "", domainEvent.Action.OccurredOn),
            ct);
}
