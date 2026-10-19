namespace Blocks.Domain;

// insight - classes, not records: record equality breaks down across an inheritance hierarchy

public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other) =>
        other is not null && other.GetType() == GetType()
        && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(1, (hash, component) => hash * 23 + (component?.GetHashCode() ?? 0));

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}

/// <summary>Wraps one string. Subclasses hide the constructor and expose a validating static Create.</summary>
public abstract class StringValueObject : IEquatable<StringValueObject>
{
    public string Value { get; protected init; } = default!;

    public bool Equals(StringValueObject? other) =>
        other is not null && other.GetType() == GetType() && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is StringValueObject other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;

    public static bool operator ==(StringValueObject? left, StringValueObject? right) => Equals(left, right);
    public static bool operator !=(StringValueObject? left, StringValueObject? right) => !Equals(left, right);
}

/// <summary>Wraps one struct value. Subclasses hide the constructor and expose a validating static Create.</summary>
public abstract class SingleValueObject<T> : IEquatable<SingleValueObject<T>>
    where T : struct
{
    public T Value { get; protected init; }

    public bool Equals(SingleValueObject<T>? other) =>
        other is not null && other.GetType() == GetType() && Value.Equals(other.Value);

    public override bool Equals(object? obj) => obj is SingleValueObject<T> other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString()!;

    public static bool operator ==(SingleValueObject<T>? left, SingleValueObject<T>? right) => Equals(left, right);
    public static bool operator !=(SingleValueObject<T>? left, SingleValueObject<T>? right) => !Equals(left, right);
}
