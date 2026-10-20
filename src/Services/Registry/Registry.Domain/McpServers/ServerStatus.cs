namespace Registry.Domain.McpServers;

public enum ServerStatus
{
    UnderReview,
    Approved,
    Quarantined
}

public enum ServerActionType
{
    Register,
    Approve,
    Quarantine
}
