namespace AgentGate.IntegrationEvents.Gateway;

/// <summary>
/// One decision the gateway made on an agent's MCP request. An observation stream: published directly, without an
/// outbox, so a lost message loses one activity row, never a governance decision (ADR 0005).
/// </summary>
/// <param name="Decision">"Allowed" or "Denied".</param>
/// <param name="Reason">Why it was denied; null when allowed.</param>
/// <param name="TraceId">The W3C trace id, to jump from the console to the distributed trace.</param>
public sealed record ToolCallObserved(Guid ObservationId, string AgentClientId, string ServerName, string Method, string? ToolName,
    string Decision, string? Reason, string? TraceId, DateTime OccurredOn);
