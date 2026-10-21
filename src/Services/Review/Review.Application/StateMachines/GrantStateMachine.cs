using Stateless;

namespace Review.Application.StateMachines;

public sealed class GrantStateMachine : IGrantStateMachine
{
    private readonly StateMachine<GrantStatus, GrantActionType> _machine;

    public GrantStateMachine(GrantStatus status, IReadOnlyList<GrantStatusTransition> transitions)
    {
        _machine = new StateMachine<GrantStatus, GrantActionType>(status);
        foreach (var transition in transitions)
            _machine.Configure(transition.CurrentStatus).Permit(transition.ActionType, transition.DestinationStatus);
    }

    public bool CanFire(GrantActionType actionType) => _machine.CanFire(actionType);
}
