namespace Review.Domain.ManifestReviews;

/// <param name="DecidedBy">The reviewers behind the decision, in order: both approvers, or the one who rejected.</param>
public sealed record ManifestReviewCompleted(ManifestReview Review, IReadOnlyList<string> DecidedBy, IAuditableAction Action) : IDomainEvent;
