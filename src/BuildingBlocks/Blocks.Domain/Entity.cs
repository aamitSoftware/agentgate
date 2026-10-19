namespace Blocks.Domain;

public interface IEntity
{
    Guid Id { get; }
}

public abstract class Entity : IEntity, IEquatable<Entity>
{
    // insight - ids are generated in code (time-ordered v7), never by the database:
    // the id exists before SaveChanges, so domain events and outbox messages can reference it,
    // and GetHashCode never changes after insert (a known pitfall of db-generated ids).
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public bool Equals(Entity? other) =>
        other is not null && (ReferenceEquals(this, other) || (other.GetType() == GetType() && other.Id == Id));

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
    public override string ToString() => $"[{GetType().Name} {Id}]";

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
