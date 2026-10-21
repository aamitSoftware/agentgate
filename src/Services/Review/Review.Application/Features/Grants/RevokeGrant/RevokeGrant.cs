using Contracts = AgentGate.IntegrationEvents.Review;

namespace Review.Application.Features.Grants.RevokeGrant;

public sealed record RevokeGrantCommand : GrantCommand, ICommand
{
    public override GrantActionType ActionType => GrantActionType.Revoke;
}

public sealed class RevokeGrantValidator : GrantCommandValidator<RevokeGrantCommand>;

public sealed class RevokeGrantHandler(GrantRepository grants, GrantStateMachineFactory stateMachineFactory) : ICommandHandler<RevokeGrantCommand>
{
    public async ValueTask<Unit> Handle(RevokeGrantCommand command, CancellationToken cancellationToken)
    {
        var grant = await grants.GetByIdOrThrowAsync(command.GrantId, cancellationToken);
        grant.Revoke(stateMachineFactory, command);
        await grants.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class PublishIntegrationEventOnGrantRevokedHandler(IPublishEndpoint publishEndpoint) : IDomainEventHandler<GrantRevoked>
{
    public Task HandleAsync(GrantRevoked domainEvent, CancellationToken ct) =>
        publishEndpoint.Publish(
            new Contracts.GrantRevoked(domainEvent.Grant.Id, domainEvent.Action.Comment ?? "", domainEvent.Action.OccurredOn), ct);
}
