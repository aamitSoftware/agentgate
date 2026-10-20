using Contracts = AgentGate.IntegrationEvents.Registry;

namespace Registry.Application.Features.McpServers.QuarantineServer;

/// <summary>Sent by the drift job (a system actor); there is no HTTP endpoint for it.</summary>
public sealed record QuarantineServerCommand : ServerCommand, ICommand
{
    [JsonIgnore] public required ToolManifest ObservedManifest { get; init; }
    public override ServerActionType ActionType => ServerActionType.Quarantine;
}

public sealed class QuarantineServerValidator : ServerCommandValidator<QuarantineServerCommand>
{
    public QuarantineServerValidator() => RuleFor(command => command.ActorId).NotEmpty();
}

public sealed class QuarantineServerHandler(McpServerRepository servers, ServerStateMachineFactory stateMachineFactory)
    : ICommandHandler<QuarantineServerCommand>
{
    public async ValueTask<Unit> Handle(QuarantineServerCommand command, CancellationToken cancellationToken)
    {
        var server = await servers.GetByIdOrThrowAsync(command.ServerId, cancellationToken);
        server.Quarantine(command.ObservedManifest, stateMachineFactory, command);
        await servers.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class PublishIntegrationEventOnServerQuarantinedHandler(IPublishEndpoint publishEndpoint)
    : IDomainEventHandler<ServerQuarantined>
{
    public Task HandleAsync(ServerQuarantined domainEvent, CancellationToken ct)
    {
        var server = domainEvent.Server;
        return publishEndpoint.Publish(
            new Contracts.ServerQuarantined(server.Id, server.Name, server.PinnedManifest!.Hash, server.PendingManifest!.Hash,
                domainEvent.Action.Comment ?? "", domainEvent.Action.OccurredOn),
            ct);
    }
}
