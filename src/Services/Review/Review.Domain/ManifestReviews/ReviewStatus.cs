namespace Review.Domain.ManifestReviews;

public enum ReviewStatus
{
    Open,
    PartiallyApproved,
    Approved,
    Rejected,
    Superseded
}

public enum ReviewActionType
{
    Open,
    Approve,
    Reject,
    Supersede
}

public enum RiskLevel
{
    Low,
    Medium,
    High
}
