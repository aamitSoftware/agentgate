namespace AgentGate.IntegrationEvents.Registry;

/// <param name="ClientId">The agent's Keycloak client id: what the gateway sees as the token's <c>azp</c>.</param>
public sealed record AgentRegistered(Guid AgentId, string Name, string ClientId, string Owner, string Purpose, DateTime OccurredOn);
