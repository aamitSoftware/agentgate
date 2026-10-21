using AgentGate.Grpc.Contracts.Registry;
using Contracts = AgentGate.IntegrationEvents.Review;

namespace Review.Application.Features.ManifestReviews.ApproveReview;

public sealed record ApproveReviewCommand : ReviewCommand, ICommand
{
    public override ReviewActionType ActionType => ReviewActionType.Approve;
}

public sealed class ApproveReviewValidator : ReviewCommandValidator<ApproveReviewCommand>;

public sealed class ApproveReviewHandler(ManifestReviewRepository reviews, IRegistryService registry,
    ReviewStateMachineFactory stateMachineFactory) : ICommandHandler<ApproveReviewCommand>
{
    public async ValueTask<Unit> Handle(ApproveReviewCommand command, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdOrThrowAsync(command.ReviewId, cancellationToken);

        // insight - only the completing approval needs Registry: it is the one that changes what agents will be shown
        var currentPendingHash = review.AwaitsFinalApproval
            ? (await registry.GetSnapshotOrThrowAsync(review.ServerId, cancellationToken)).PendingManifestHash
            : null;

        review.Approve(currentPendingHash, stateMachineFactory, command);
        await reviews.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class PublishIntegrationEventOnManifestReviewCompletedHandler(IPublishEndpoint publishEndpoint)
    : IDomainEventHandler<ManifestReviewCompleted>
{
    public Task HandleAsync(ManifestReviewCompleted domainEvent, CancellationToken ct)
    {
        var review = domainEvent.Review;
        return publishEndpoint.Publish(
            new Contracts.ManifestReviewCompleted(review.Id, review.ServerId, review.ManifestHash, review.Status.ToString(),
                review.RiskLevel.ToString(), domainEvent.DecidedBy, domainEvent.Action.Comment, domainEvent.Action.OccurredOn),
            ct);
    }
}
