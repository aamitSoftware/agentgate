namespace Blocks.Domain;

public interface IAggregateRoot : IEntity
{
    string CreatedBy { get; }
    DateTime CreatedOn { get; }
    string? LastModifiedBy { get; }
    DateTime? LastModifiedOn { get; }

    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

public abstract class AggregateRoot : Entity, IAggregateRoot
{
    // insight - audit lives only on aggregates: child entities are always saved as part of their aggregate
    public string CreatedBy { get; protected init; } = default!;
    public DateTime CreatedOn { get; protected init; }
    public string? LastModifiedBy { get; protected set; }
    public DateTime? LastModifiedOn { get; protected set; }

    // insight - events are an aggregate-owned side channel: observers can read and clear, only the aggregate can add
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void MarkModified(IAuditableAction action)
    {
        LastModifiedBy = action.ActorId;
        LastModifiedOn = action.OccurredOn;
    }
}
