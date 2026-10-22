namespace Review.Application.Features.ManifestReviews.GetReviews;

public sealed record ReviewSummaryDto(Guid Id, Guid ServerId, string ServerName, string ManifestHash, string? PreviousHash,
    RiskLevel RiskLevel, int FindingCount, ReviewStatus Status, string? FirstApproverId, DateTime CreatedOn);

public sealed record AuditEntryDto(string Action, string ActorId, DateTime OccurredOn, string? Comment, string? FromStatus, string ToStatus);

public sealed record ReviewDetailDto(ReviewSummaryDto Summary, string SubmissionReason, IReadOnlyList<RiskFinding> Findings,
    string ToolsJson, string? PreviousToolsJson, IReadOnlyList<AuditEntryDto> AuditEntries);

/// <summary>The approvals queue: newest first, optionally narrowed to one status or one server.</summary>
public sealed record GetReviewsQuery(ReviewStatus? Status, Guid? ServerId) : IQuery<IReadOnlyList<ReviewSummaryDto>>;

public sealed record GetReviewQuery(Guid ReviewId) : IQuery<ReviewDetailDto>;

public sealed class GetReviewsHandler(ReviewDbContext db) : IQueryHandler<GetReviewsQuery, IReadOnlyList<ReviewSummaryDto>>
{
    public async ValueTask<IReadOnlyList<ReviewSummaryDto>> Handle(GetReviewsQuery query, CancellationToken cancellationToken)
    {
        var reviews = db.ManifestReviews.AsNoTracking();
        if (query.Status is { } status)
            reviews = reviews.Where(review => review.Status == status);
        if (query.ServerId is { } serverId)
            reviews = reviews.Where(review => review.ServerId == serverId);

        // ponytail: unpaged; the queue is small until there are hundreds of servers
        return (await reviews.OrderByDescending(review => review.CreatedOn).ToListAsync(cancellationToken)).Select(Summary).ToList();
    }

    internal static ReviewSummaryDto Summary(ManifestReview review) =>
        new(review.Id, review.ServerId, review.ServerName, review.ManifestHash, review.PreviousHash, review.RiskLevel,
            review.Findings.Count, review.Status, review.FirstApproverId, review.CreatedOn);
}

public sealed class GetReviewHandler(ManifestReviewRepository reviews) : IQueryHandler<GetReviewQuery, ReviewDetailDto>
{
    public async ValueTask<ReviewDetailDto> Handle(GetReviewQuery query, CancellationToken cancellationToken)
    {
        var review = (await reviews.Query().AsNoTracking().SingleOrDefaultAsync(review => review.Id == query.ReviewId, cancellationToken))
            .OrThrowNotFound($"Review {query.ReviewId} not found.");

        return new ReviewDetailDto(GetReviewsHandler.Summary(review), review.SubmissionReason, review.Findings, review.ToolsJson,
            review.PreviousToolsJson,
            [.. review.AuditEntries.OrderBy(entry => entry.OccurredOn).Select(entry => new AuditEntryDto(entry.Action, entry.ActorId,
                entry.OccurredOn, entry.Comment, entry.FromStatus?.ToString(), entry.ToStatus.ToString()))]);
    }
}
