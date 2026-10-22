using AgentGate.Grpc.Contracts.Registry;
using Contracts = AgentGate.IntegrationEvents.Review;

namespace Review.Application.Features.Grants.ApproveGrant;

public sealed record ApproveGrantCommand : GrantCommand, ICommand
{
    public override GrantActionType ActionType => GrantActionType.Approve;
}

public sealed class ApproveGrantValidator : GrantCommandValidator<ApproveGrantCommand>;

public sealed class ApproveGrantHandler(GrantRepository grants, IRegistryService registry, GrantStateMachineFactory stateMachineFactory)
    : ICommandHandler<ApproveGrantCommand>
{
    private const string Approved = "Approved";

    public async ValueTask<Unit> Handle(ApproveGrantCommand command, CancellationToken cancellationToken)
    {
        var grant = await grants.GetByIdOrThrowAsync(command.GrantId, cancellationToken);

        CurrentServerState? currentServer = null;
        if (grant.AwaitsFinalApproval)
        {
            var snapshot = await registry.GetSnapshotOrThrowAsync(grant.ServerId, cancellationToken);
            currentServer = new CurrentServerState(snapshot.Status == Approved, snapshot.PinnedToolNames);
        }

        grant.Approve(currentServer, stateMachineFactory, command);
        await grants.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class PublishIntegrationEventOnGrantActivatedHandler(IPublishEndpoint publishEndpoint) : IDomainEventHandler<GrantActivated>
{
    public Task HandleAsync(GrantActivated domainEvent, CancellationToken ct)
    {
        var grant = domainEvent.Grant;
        return publishEndpoint.Publish(
            new Contracts.GrantActivated(grant.Id, grant.AgentId, grant.AgentClientId, grant.ServerId, grant.ServerName, grant.Tools,
                grant.ExpiresAt, domainEvent.Action.OccurredOn),
            ct);
    }
}
