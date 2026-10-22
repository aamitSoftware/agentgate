using AgentGate.IntegrationEvents.Gateway;
using Microsoft.Extensions.Logging;
using Review.Application.Features.Grants.SuspendGrant;

namespace Review.Application.Features.Grants.SuspendOnAnomaly;

/// <summary>
/// The gateway caught an agent probing for tools it wasn't given and has blocked it locally; Review owns grants, so it
/// suspends every active grant of that agent. A reviewer resumes them (which also lifts the gateway's block) or revokes.
/// </summary>
public sealed class AnomalyDetectedConsumer(GrantRepository grants, GrantStateMachineFactory stateMachineFactory,
    ILogger<AnomalyDetectedConsumer> logger) : IConsumer<AnomalyDetected>
{
    public async Task Consume(ConsumeContext<AnomalyDetected> context)
    {
        var anomaly = context.Message;
        var active = await grants.Query()
            .Where(grant => grant.AgentClientId == anomaly.AgentClientId && grant.Status == GrantStatus.Active)
            .ToListAsync(context.CancellationToken);

        var reason = $"Auto-suspended: {anomaly.DeniedCount} denied calls in {anomaly.WindowSeconds} s " +
                     $"({string.Join(", ", anomaly.RecentTargets.Take(3))}). Possible prompt injection.";
        foreach (var grant in active)
            grant.Suspend(stateMachineFactory, new SuspendGrantCommand
            {
                GrantId = grant.Id,
                ActorId = SystemActors.AnomalyDetector,
                Comment = reason.Length > MaxLength.C512 ? reason[..MaxLength.C512] : reason
            });

        await grants.SaveChangesAsync(context.CancellationToken); // each suspension's GrantSuspended goes out in this commit
        logger.LogWarning("Anomaly {AnomalyId}: suspended {Count} grant(s) of {AgentClientId}", anomaly.AnomalyId, active.Count,
            anomaly.AgentClientId);
    }
}
