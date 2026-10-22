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

/// <summary>
/// The gateway saw an agent misbehave and has already blocked it locally; Review, which owns grants, suspends them.
/// Published as soon as the threshold is crossed, retried until the broker accepts it.
/// </summary>
/// <param name="Kind">What was detected; "DeniedCallSpike" in v1.</param>
/// <param name="RecentTargets">The server/tool pairs of the denied calls, newest first, for the reviewer.</param>
public sealed record AnomalyDetected(Guid AnomalyId, string AgentClientId, string Kind, int DeniedCount, int WindowSeconds,
    IReadOnlyList<string> RecentTargets, DateTime OccurredOn);
