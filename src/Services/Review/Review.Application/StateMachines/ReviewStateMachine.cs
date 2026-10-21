using Stateless;

namespace Review.Application.StateMachines;

public sealed class ReviewStateMachine : IReviewStateMachine
{
    private readonly StateMachine<ReviewStatus, ReviewActionType> _machine;

    public ReviewStateMachine(ReviewStatus status, IReadOnlyList<ReviewStatusTransition> transitions)
    {
        _machine = new StateMachine<ReviewStatus, ReviewActionType>(status);
        foreach (var transition in transitions)
            _machine.Configure(transition.CurrentStatus).Permit(transition.ActionType, transition.DestinationStatus);
    }

    public bool CanFire(ReviewActionType actionType) => _machine.CanFire(actionType);
}
