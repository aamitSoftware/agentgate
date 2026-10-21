namespace Review.Domain.Grants;

/// <summary>Raised on activation and on resume: the grant is in force.</summary>
public sealed record GrantActivated(Grant Grant, IAuditableAction Action) : IDomainEvent;

public sealed record GrantSuspended(Grant Grant, IAuditableAction Action) : IDomainEvent;

public sealed record GrantRevoked(Grant Grant, IAuditableAction Action) : IDomainEvent;
