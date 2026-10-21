namespace Review.Persistence.Repositories;

public sealed class ManifestReviewRepository(ReviewDbContext dbContext)
    : RepositoryBase<ReviewDbContext, ManifestReview>(dbContext)
{
    public override IQueryable<ManifestReview> Query() => Entities.Include(review => review.AuditEntries);

    /// <summary>Reviews of this server that can still be decided.</summary>
    public Task<List<ManifestReview>> GetUndecidedAsync(Guid serverId, CancellationToken ct = default) =>
        Query().Where(review => review.ServerId == serverId
                                && (review.Status == ReviewStatus.Open || review.Status == ReviewStatus.PartiallyApproved))
            .ToListAsync(ct);
}

public sealed class GrantRepository(ReviewDbContext dbContext)
    : RepositoryBase<ReviewDbContext, Grant>(dbContext)
{
    public override IQueryable<Grant> Query() => Entities.Include(grant => grant.AuditEntries);
}
