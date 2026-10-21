using Contracts = AgentGate.IntegrationEvents.Registry;

namespace Registry.Application.Features.McpServers.SubmitManifest;

/// <summary>Raised on registration and on drift: either way, Review opens a review of the pending manifest.</summary>
public sealed class PublishIntegrationEventOnManifestSubmittedHandler(IPublishEndpoint publishEndpoint)
    : IDomainEventHandler<ManifestSubmitted>
{
    public Task HandleAsync(ManifestSubmitted domainEvent, CancellationToken ct)
    {
        var server = domainEvent.Server;
        var pending = server.PendingManifest!;
        var previous = server.PinnedManifest;
        return publishEndpoint.Publish(
            new Contracts.ManifestSubmitted(server.Id, server.Name, pending.Hash, pending.ToolsJson, previous?.Hash, previous?.ToolsJson,
                domainEvent.Action.Comment ?? domainEvent.Action.Action, domainEvent.Action.OccurredOn),
            ct);
    }
}
