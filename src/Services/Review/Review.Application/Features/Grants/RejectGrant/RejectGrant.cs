namespace Review.Application.Features.Grants.RejectGrant;

public sealed record RejectGrantCommand : GrantCommand, ICommand
{
    public override GrantActionType ActionType => GrantActionType.Reject;
}

public sealed class RejectGrantValidator : GrantCommandValidator<RejectGrantCommand>
{
    public RejectGrantValidator() => RuleFor(command => command.Comment).NotEmpty();
}

public sealed class RejectGrantHandler(GrantRepository grants, GrantStateMachineFactory stateMachineFactory) : ICommandHandler<RejectGrantCommand>
{
    public async ValueTask<Unit> Handle(RejectGrantCommand command, CancellationToken cancellationToken)
    {
        var grant = await grants.GetByIdOrThrowAsync(command.GrantId, cancellationToken);
        grant.Reject(stateMachineFactory, command);
        await grants.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
