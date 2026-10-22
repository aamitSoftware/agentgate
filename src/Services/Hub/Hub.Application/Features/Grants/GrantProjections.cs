using AgentGate.IntegrationEvents.Review;

namespace Hub.Application.Features.Grants;

public sealed class GrantActivatedConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<GrantActivated>
{
    public async Task Consume(ConsumeContext<GrantActivated> context)
    {
        var message = context.Message;
        var grant = await db.Grants.FindAsync([message.GrantId], context.CancellationToken);
        if (grant is null)
        {
            grant = new GrantView { Id = message.GrantId, StatusAsOf = DateTime.MinValue };
            db.Grants.Add(grant);
        }

        grant.AgentId = message.AgentId;
        grant.AgentClientId = message.AgentClientId;
        grant.ServerId = message.ServerId;
        grant.ServerName = message.ServerName;
        grant.Tools = [.. message.Tools];
        grant.ExpiresAt = message.ExpiresAt;
        if (message.OccurredOn >= grant.StatusAsOf)
        {
            grant.Status = "Active";
            grant.StatusAsOf = message.OccurredOn;
        }

        var entry = db.AddTimeline(context, message.OccurredOn,
            $"Grant in force: {message.AgentClientId} may call {string.Join(", ", message.Tools)} on {message.ServerName}" +
            (message.ExpiresAt is { } expiresAt ? $" until {expiresAt:yyyy-MM-dd HH:mm} UTC." : "."),
            Severities.Info, serverId: message.ServerId, agentId: message.AgentId, grantId: message.GrantId);
        await db.SaveChangesAsync(context.CancellationToken);
        feed.Publish(ActivityItem.From(entry));
    }
}

public sealed class GrantSuspendedConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<GrantSuspended>
{
    public Task Consume(ConsumeContext<GrantSuspended> context) =>
        GrantStatusChange.ApplyAsync(db, feed, context, context.Message.GrantId, "Suspended", context.Message.OccurredOn,
            $"suspended: {context.Message.Reason}");
}

public sealed class GrantRevokedConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<GrantRevoked>
{
    public Task Consume(ConsumeContext<GrantRevoked> context) =>
        GrantStatusChange.ApplyAsync(db, feed, context, context.Message.GrantId, "Revoked", context.Message.OccurredOn,
            $"revoked: {context.Message.Reason}");
}

internal static class GrantStatusChange
{
    public static async Task ApplyAsync<TMessage>(HubDbContext db, ActivityFeed feed, ConsumeContext<TMessage> context, Guid grantId,
        string status, DateTime occurredOn, string what) where TMessage : class
    {
        // a grant is only suspended or revoked after it was active; retried until that projection exists
        var grant = (await db.Grants.FindAsync([grantId], context.CancellationToken))
            .OrThrowNotFound($"Grant {grantId} is not projected yet.");
        if (occurredOn >= grant.StatusAsOf)
        {
            grant.Status = status;
            grant.StatusAsOf = occurredOn;
        }

        var entry = db.AddTimeline(context, occurredOn, $"Grant for {grant.AgentClientId} on {grant.ServerName} {what}", Severities.Warning,
            serverId: grant.ServerId, agentId: grant.AgentId, grantId: grant.Id);
        await db.SaveChangesAsync(context.CancellationToken);
        feed.Publish(ActivityItem.From(entry));
    }
}
