namespace Review.Domain.ManifestReviews;

/// <summary>One legal move: a row of the seeded transition table.</summary>
public sealed class ReviewStatusTransition
{
    public ReviewStatus CurrentStatus { get; init; }
    public ReviewActionType ActionType { get; init; }
    public ReviewStatus DestinationStatus { get; init; }
}

public interface IReviewStateMachine
{
    bool CanFire(ReviewActionType actionType);
}

public delegate IReviewStateMachine ReviewStateMachineFactory(ReviewStatus status);

public static class ReviewStateMachineFactoryExtensions
{
    public static void ValidateTransition(this ReviewStateMachineFactory factory, ReviewStatus status, ReviewActionType actionType)
    {
        if (!factory(status).CanFire(actionType))
            throw new DomainException($"Action {actionType} is not allowed while the review is {status}.");
    }
}
