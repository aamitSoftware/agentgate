namespace Registry.Domain.McpServers;

/// <summary>One governance action on a server: who, when, what, why, and the status it moved between.</summary>
public sealed class ServerAuditEntry : Entity
{
    private ServerAuditEntry() { }

    public string Action { get; private init; } = default!;
    public string ActorId { get; private init; } = default!;
    public DateTime OccurredOn { get; private init; }
    public string? Comment { get; private init; }
    public ServerStatus? FromStatus { get; private init; }
    public ServerStatus ToStatus { get; private init; }

    internal static ServerAuditEntry Create(ServerStatus? fromStatus, ServerStatus toStatus, IAuditableAction action) => new()
    {
        Action = action.Action,
        ActorId = action.ActorId,
        OccurredOn = action.OccurredOn,
        Comment = action.Comment,
        FromStatus = fromStatus,
        ToStatus = toStatus
    };
}
