namespace Registry.Domain.Agents;

public sealed record AgentRegistered(Agent Agent, IAuditableAction Action) : IDomainEvent;
