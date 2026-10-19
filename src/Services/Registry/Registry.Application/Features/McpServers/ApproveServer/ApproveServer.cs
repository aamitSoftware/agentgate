using Contracts = AgentGate.IntegrationEvents.Registry;

namespace Registry.Application.Features.McpServers.ApproveServer;

// ponytail: a single admin approves for now; M3 moves approval to Review (risk scan + four-eyes) and this becomes a consumer
public sealed record ApproveServerCommand : ServerCommand, ICommand
{
    public override ServerActionType ActionType => ServerActionType.Approve;
}

public sealed class ApproveServerValidator : ServerCommandValidator<ApproveServerCommand>;

public sealed class ApproveServerHandler(McpServerRepository servers, ServerStateMachineFactory stateMachineFactory)
    : ICommandHandler<ApproveServerCommand>
{
    public async ValueTask<Unit> Handle(ApproveServerCommand command, CancellationToken cancellationToken)
    {
        var server = await servers.GetByIdOrThrowAsync(command.ServerId, cancellationToken);
        server.Approve(stateMachineFactory, command);
        await servers.SaveChangesAsync(cancellationToken);
        return Unit.Value;
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
