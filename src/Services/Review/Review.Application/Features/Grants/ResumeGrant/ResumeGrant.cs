namespace Review.Application.Features.Grants.ResumeGrant;

public sealed record ResumeGrantCommand : GrantCommand, ICommand
{
    public override GrantActionType ActionType => GrantActionType.Resume;
}

public sealed class ResumeGrantValidator : GrantCommandValidator<ResumeGrantCommand>;

/// <summary>Resuming re-raises GrantActivated, so the gateway's copy converges back to active.</summary>
public sealed class ResumeGrantHandler(GrantRepository grants, GrantStateMachineFactory stateMachineFactory) : ICommandHandler<ResumeGrantCommand>
{
    public async ValueTask<Unit> Handle(ResumeGrantCommand command, CancellationToken cancellationToken)
    {
        var grant = await grants.GetByIdOrThrowAsync(command.GrantId, cancellationToken);
        grant.Resume(stateMachineFactory, command);
        await grants.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
