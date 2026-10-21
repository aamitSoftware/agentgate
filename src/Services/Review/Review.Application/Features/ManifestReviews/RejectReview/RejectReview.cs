namespace Review.Application.Features.ManifestReviews.RejectReview;

public sealed record RejectReviewCommand : ReviewCommand, ICommand
{
    public override ReviewActionType ActionType => ReviewActionType.Reject;
}

public sealed class RejectReviewValidator : ReviewCommandValidator<RejectReviewCommand>
{
    public RejectReviewValidator() => RuleFor(command => command.Comment).NotEmpty();
}

/// <summary>One reviewer is enough to reject: refusing is the safe direction. No Registry check is needed for it.</summary>
public sealed class RejectReviewHandler(ManifestReviewRepository reviews, ReviewStateMachineFactory stateMachineFactory)
    : ICommandHandler<RejectReviewCommand>
{
    public async ValueTask<Unit> Handle(RejectReviewCommand command, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdOrThrowAsync(command.ReviewId, cancellationToken);
        review.Reject(stateMachineFactory, command);
        await reviews.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
