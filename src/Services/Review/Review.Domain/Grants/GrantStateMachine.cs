namespace Review.Domain.Grants;

/// <summary>One legal move: a row of the seeded transition table.</summary>
public sealed class GrantStatusTransition
{
    public GrantStatus CurrentStatus { get; init; }
    public GrantActionType ActionType { get; init; }
    public GrantStatus DestinationStatus { get; init; }
}

public interface IGrantStateMachine
{
    bool CanFire(GrantActionType actionType);
}

public delegate IGrantStateMachine GrantStateMachineFactory(GrantStatus status);

public static class GrantStateMachineFactoryExtensions
{
    public static void ValidateTransition(this GrantStateMachineFactory factory, GrantStatus status, GrantActionType actionType)
    {
        if (!factory(status).CanFire(actionType))
            throw new DomainException($"Action {actionType} is not allowed while the grant is {status}.");
    }
}
