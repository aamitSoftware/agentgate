namespace Registry.Domain.McpServers;

public enum ServerStatus
{
    UnderReview,
    Approved,
    Quarantined,
    Rejected
}

public enum ServerActionType
{
    Register,
    Approve,
    Quarantine,
    Reject
}
