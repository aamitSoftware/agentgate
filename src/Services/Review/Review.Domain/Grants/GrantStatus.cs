namespace Review.Domain.Grants;

// insight - no Expired status: the gateway compares ExpiresAt itself, so nothing has to run on a schedule
public enum GrantStatus
{
    Requested,
    PendingApproval,
    Active,
    Suspended,
    Revoked,
    Rejected
}

public enum GrantActionType
{
    Request,
    Approve,
    Reject,
    Suspend,
    Resume,
    Revoke
}
