namespace Hub.Application.Features.Activity;

public static class TimelineExtensions
{
    /// <summary>
    /// Files a governance event. The id is the message id, so a redelivery that slipped past the inbox would collide
    /// instead of duplicating the entry.
    /// </summary>
    public static TimelineEntry AddTimeline<TMessage>(this HubDbContext db, ConsumeContext<TMessage> context, DateTime occurredOn,
        string summary, string severity, Guid? serverId = null, Guid? agentId = null, Guid? grantId = null, Guid? reviewId = null,
        string? actorId = null) where TMessage : class
    {
        var entry = new TimelineEntry
        {
            Id = context.MessageId ?? NewId.NextGuid(),
            OccurredOn = occurredOn,
            Kind = typeof(TMessage).Name,
            Summary = summary.Length > MaxLength.C1024 ? summary[..MaxLength.C1024] : summary,
            Severity = severity,
            ActorId = actorId,
            ServerId = serverId,
            AgentId = agentId,
            GrantId = grantId,
            ReviewId = reviewId
        };
        db.Timeline.Add(entry);
        return entry;
    }

    public static string Short(this string hash) => hash.Length > 12 ? hash[..12] : hash;
}

/// <summary>Gateway decisions: stored for the activity screen and pushed to open streams.</summary>
public sealed class ToolCallObservedConsumer(HubDbContext db, ActivityFeed feed)
    : IConsumer<AgentGate.IntegrationEvents.Gateway.ToolCallObserved>
{
    public async Task Consume(ConsumeContext<AgentGate.IntegrationEvents.Gateway.ToolCallObserved> context)
    {
        var observed = context.Message;
        var call = new ToolCallView
        {
            Id = observed.ObservationId,
            OccurredOn = observed.OccurredOn,
            AgentClientId = observed.AgentClientId,
            ServerName = observed.ServerName,
            Method = observed.Method,
            ToolName = observed.ToolName,
            Decision = observed.Decision,
            Reason = observed.Reason is { Length: > MaxLength.C512 } reason ? reason[..MaxLength.C512] : observed.Reason,
            TraceId = observed.TraceId
        };
        db.ToolCalls.Add(call);
        await db.SaveChangesAsync(context.CancellationToken);
        // ponytail: the feed is pushed before the inbox transaction commits; a commit failure means a row on screen
        // that is not stored, and the retry pushes it again (the console de-duplicates by id)
        feed.Publish(ActivityItem.From(call));
    }
}
