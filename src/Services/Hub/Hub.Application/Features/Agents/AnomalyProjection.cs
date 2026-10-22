using AgentGate.IntegrationEvents.Gateway;

namespace Hub.Application.Features.Agents;

/// <summary>The loudest event in the console: an agent blocked for probing tools it wasn't given.</summary>
public sealed class AnomalyDetectedConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<AnomalyDetected>
{
    public async Task Consume(ConsumeContext<AnomalyDetected> context)
    {
        var anomaly = context.Message;
        var agent = await db.Agents.AsNoTracking().SingleOrDefaultAsync(agent => agent.ClientId == anomaly.AgentClientId, context.CancellationToken);

        var entry = db.AddTimeline(context, anomaly.OccurredOn,
            $"Anomaly: {anomaly.AgentClientId} had {anomaly.DeniedCount} calls denied in {anomaly.WindowSeconds} s " +
            $"({string.Join(", ", anomaly.RecentTargets.Take(3))}). Blocked at the gateway; its grants are being suspended.",
            Severities.Critical, agentId: agent?.Id, actorId: "system:anomaly-detector");
        await db.SaveChangesAsync(context.CancellationToken);
        feed.Publish(ActivityItem.From(entry));
    }
}
