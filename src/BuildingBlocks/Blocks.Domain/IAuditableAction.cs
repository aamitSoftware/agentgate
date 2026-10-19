namespace Blocks.Domain;

/// <summary>
/// Every state change is carried by an action: who did it, when, what, and why.
/// The same object drives the transition check, the audit trail and the domain event.
/// </summary>
public interface IAuditableAction
{
    // insight - provenance is stamped by the pipeline from the JWT ('sub') or set explicitly
    // to a system actor (e.g. "system:drift-detector"); it is never trusted from the request body.
    string ActorId { get; set; }
    DateTime OccurredOn { get; }
    string Action { get; }
    string? Comment { get; }
}

public interface IAuditableAction<out TActionType> : IAuditableAction
    where TActionType : struct, Enum
{
    TActionType ActionType { get; }

    // insight - default interface implementation derives the audit label from the typed action
    string IAuditableAction.Action => ActionType.ToString();
}
