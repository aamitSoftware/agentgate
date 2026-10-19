namespace Registry.Domain.McpServers;

public sealed record ServerApproved(McpServer Server, IAuditableAction Action) : IDomainEvent;

public sealed record ServerQuarantined(McpServer Server, IAuditableAction Action) : IDomainEvent;
