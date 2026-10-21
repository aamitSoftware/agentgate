namespace Review.Domain.Grants;

/// <summary>One governance action on a grant: who, when, what, why, and the status it moved between.</summary>
public sealed class GrantAuditEntry : Entity
{
    private GrantAuditEntry() { }

    public string Action { get; private init; } = default!;
    public string ActorId { get; private init; } = default!;
    public DateTime OccurredOn { get; private init; }
    public string? Comment { get; private init; }
    public GrantStatus? FromStatus { get; private init; }
    public GrantStatus ToStatus { get; private init; }

    internal static GrantAuditEntry Create(GrantStatus? fromStatus, GrantStatus toStatus, IAuditableAction action) => new()
    {
        Action = action.Action,
        ActorId = action.ActorId,
        OccurredOn = action.OccurredOn,
        Comment = action.Comment,
        FromStatus = fromStatus,
        ToStatus = toStatus
    };
}
