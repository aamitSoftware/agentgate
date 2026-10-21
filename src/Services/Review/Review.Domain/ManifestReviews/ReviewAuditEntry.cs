namespace Review.Domain.ManifestReviews;

/// <summary>One governance action on a review: who, when, what, why, and the status it moved between.</summary>
public sealed class ReviewAuditEntry : Entity
{
    private ReviewAuditEntry() { }

    public string Action { get; private init; } = default!;
    public string ActorId { get; private init; } = default!;
    public DateTime OccurredOn { get; private init; }
    public string? Comment { get; private init; }
    public ReviewStatus? FromStatus { get; private init; }
    public ReviewStatus ToStatus { get; private init; }

    internal static ReviewAuditEntry Create(ReviewStatus? fromStatus, ReviewStatus toStatus, IAuditableAction action) => new()
    {
        Action = action.Action,
        ActorId = action.ActorId,
        OccurredOn = action.OccurredOn,
        Comment = action.Comment,
        FromStatus = fromStatus,
        ToStatus = toStatus
    };
}
