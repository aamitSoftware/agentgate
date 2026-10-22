using System.Collections.Concurrent;
using AgentGate.IntegrationEvents.Review;

namespace Gateway.API.Features.Grants;

/// <summary>One grant as the gateway knows it. <see cref="InForce"/> is false once suspended or revoked.</summary>
public sealed record GrantEntry(Guid GrantId, string AgentClientId, string ServerName, IReadOnlySet<string> Tools, DateTime? ExpiresAt,
    bool InForce, DateTime AsOf);

/// <summary>
/// Which agent may call which tool on which server: the gateway's local copy of Review's grants, built only from
/// events. Deny unless an in-force, unexpired grant names the tool. The request path never calls Review.
/// </summary>
// ponytail: in memory, so a restarted gateway denies every call until grants are re-announced (fail closed), and
// IsGranted scans every grant; index by (agent, server) once there are thousands
public sealed class GrantTable(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<Guid, GrantEntry> _grants = new();

    public bool IsGranted(string agentClientId, string serverName, string tool)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        // insight - expiry is checked here, at use, so no scheduler has to flip grants to Expired on time
        return _grants.Values.Any(grant => grant.InForce
                                           && (grant.ExpiresAt is null || grant.ExpiresAt > now)
                                           && grant.AgentClientId == agentClientId
                                           && grant.ServerName == serverName
                                           && grant.Tools.Contains(tool));
    }

    public void Activate(GrantActivated activated) =>
        Apply(activated.GrantId, activated.OccurredOn, _ => new GrantEntry(activated.GrantId, activated.AgentClientId, activated.ServerName,
            activated.Tools.ToHashSet(StringComparer.Ordinal), activated.ExpiresAt, InForce: true, activated.OccurredOn));

    public void Suspend(GrantSuspended suspended) => Block(suspended.GrantId, suspended.OccurredOn);

    public void Revoke(GrantRevoked revoked) => Block(revoked.GrantId, revoked.OccurredOn);

    private void Block(Guid grantId, DateTime occurredOn) =>
        Apply(grantId, occurredOn, current => current is null
            // never seen active (e.g. after a restart): remember the block so an older activation can't lift it
            ? new GrantEntry(grantId, "", "", new HashSet<string>(), null, InForce: false, occurredOn)
            : current with { InForce = false, AsOf = occurredOn });

    // insight - overwrite, never increment: a redelivered event converges to the same state (no inbox needed), and an
    // event older than what is already applied is dropped (activation and suspension travel on separate queues)
    private void Apply(Guid grantId, DateTime occurredOn, Func<GrantEntry?, GrantEntry> next) =>
        _grants.AddOrUpdate(grantId, _ => next(null), (_, current) => occurredOn >= current.AsOf ? next(current) : current);
}
