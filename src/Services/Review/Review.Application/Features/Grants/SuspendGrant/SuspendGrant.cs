using Contracts = AgentGate.IntegrationEvents.Review;

namespace Review.Application.Features.Grants.SuspendGrant;

public sealed record SuspendGrantCommand : GrantCommand, ICommand
{
    public override GrantActionType ActionType => GrantActionType.Suspend;
}

public sealed class SuspendGrantValidator : GrantCommandValidator<SuspendGrantCommand>;

public sealed class SuspendGrantHandler(GrantRepository grants, GrantStateMachineFactory stateMachineFactory) : ICommandHandler<SuspendGrantCommand>
{
    public async ValueTask<Unit> Handle(SuspendGrantCommand command, CancellationToken cancellationToken)
    {
        var grant = await grants.GetByIdOrThrowAsync(command.GrantId, cancellationToken);
        grant.Suspend(stateMachineFactory, command);
        await grants.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class PublishIntegrationEventOnGrantSuspendedHandler(IPublishEndpoint publishEndpoint) : IDomainEventHandler<GrantSuspended>
{
    public Task HandleAsync(GrantSuspended domainEvent, CancellationToken ct) =>
        publishEndpoint.Publish(
            new Contracts.GrantSuspended(domainEvent.Grant.Id, domainEvent.Action.Comment ?? "", domainEvent.Action.OccurredOn), ct);
}
