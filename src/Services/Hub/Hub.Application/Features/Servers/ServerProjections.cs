using AgentGate.IntegrationEvents.Registry;
using AgentGate.IntegrationEvents.Review;

namespace Hub.Application.Features.Servers;

internal static class ServerRows
{
    /// <summary>Get-or-create: the first event about a server may be any of them (queues don't preserve order across types).</summary>
    public static async Task<ServerView> GetOrAddAsync(this HubDbContext db, Guid serverId, string name, DateTime occurredOn, CancellationToken ct)
    {
        if (await db.Servers.FindAsync([serverId], ct) is { } server)
            return server;

        server = new ServerView { Id = serverId, Name = name, Status = "UnderReview", StatusAsOf = DateTime.MinValue, FirstSeenOn = occurredOn };
        db.Servers.Add(server);
        return server;
    }

    /// <summary>A status from an older event than the one already applied is ignored.</summary>
    public static void SetStatus(this ServerView server, string status, DateTime occurredOn)
    {
        if (occurredOn < server.StatusAsOf)
            return;
        server.Status = status;
        server.StatusAsOf = occurredOn;
    }
}

public sealed class ManifestSubmittedConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<ManifestSubmitted>
{
    public async Task Consume(ConsumeContext<ManifestSubmitted> context)
    {
        var message = context.Message;
        var server = await db.GetOrAddAsync(message.ServerId, message.Name, message.OccurredOn, context.CancellationToken);
        server.PendingHash = message.ManifestHash;

        var entry = db.AddTimeline(context, message.OccurredOn,
            message.PreviousHash is null
                ? $"Registered '{message.Name}'; manifest {message.ManifestHash.Short()} submitted for review."
                : $"Manifest {message.ManifestHash.Short()} submitted for re-review (pinned {message.PreviousHash.Short()}): {message.Reason}",
            Severities.Info, serverId: message.ServerId);
        await db.SaveChangesAsync(context.CancellationToken);
        feed.Publish(ActivityItem.From(entry));
    }
}

public sealed class ServerApprovedConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<ServerApproved>
{
    public async Task Consume(ConsumeContext<ServerApproved> context)
    {
        var message = context.Message;
        var server = await db.GetOrAddAsync(message.ServerId, message.Name, message.OccurredOn, context.CancellationToken);
        server.Url = message.Url;
        server.PinnedHash = message.ManifestHash;
        server.PinnedToolsJson = message.ToolsJson;
        if (server.PendingHash == message.ManifestHash)
            server.PendingHash = null;
        server.SetStatus("Approved", message.OccurredOn);

        var entry = db.AddTimeline(context, message.OccurredOn, $"Approved; manifest {message.ManifestHash.Short()} pinned.",
            Severities.Info, serverId: message.ServerId);
        await db.SaveChangesAsync(context.CancellationToken);
        feed.Publish(ActivityItem.From(entry));
    }
}

public sealed class ServerQuarantinedConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<ServerQuarantined>
{
    public async Task Consume(ConsumeContext<ServerQuarantined> context)
    {
        var message = context.Message;
        var server = await db.GetOrAddAsync(message.ServerId, message.Name, message.OccurredOn, context.CancellationToken);
        server.PendingHash = message.ObservedHash;
        server.SetStatus("Quarantined", message.OccurredOn);

        var entry = db.AddTimeline(context, message.OccurredOn, $"Quarantined: {message.Reason}", Severities.Critical,
            serverId: message.ServerId);
        await db.SaveChangesAsync(context.CancellationToken);
        feed.Publish(ActivityItem.From(entry));
    }
}

public sealed class ServerRejectedConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<ServerRejected>
{
    public async Task Consume(ConsumeContext<ServerRejected> context)
    {
        var message = context.Message;
        var server = await db.GetOrAddAsync(message.ServerId, message.Name, message.OccurredOn, context.CancellationToken);
        if (server.PendingHash == message.RejectedHash)
            server.PendingHash = null;
        server.SetStatus("Rejected", message.OccurredOn);

        var entry = db.AddTimeline(context, message.OccurredOn, $"Manifest {message.RejectedHash.Short()} rejected: {message.Reason}",
            Severities.Warning, serverId: message.ServerId);
        await db.SaveChangesAsync(context.CancellationToken);
        feed.Publish(ActivityItem.From(entry));
    }
}

public sealed class ManifestReviewOpenedConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<ManifestReviewOpened>
{
    public async Task Consume(ConsumeContext<ManifestReviewOpened> context)
    {
        var message = context.Message;
        var server = await db.GetOrAddAsync(message.ServerId, message.ServerName, message.OccurredOn, context.CancellationToken);
        server.OpenReviewId = message.ReviewId;
        server.OpenReviewRisk = message.RiskLevel;

        var severity = message.RiskLevel switch { "High" => Severities.Critical, "Medium" => Severities.Warning, _ => Severities.Info };
        var entry = db.AddTimeline(context, message.OccurredOn,
            $"Review opened for manifest {message.ManifestHash.Short()}: risk {message.RiskLevel}, {message.FindingCount} finding(s).",
            severity, serverId: message.ServerId, reviewId: message.ReviewId);
        await db.SaveChangesAsync(context.CancellationToken);
        feed.Publish(ActivityItem.From(entry));
    }
}

public sealed class ManifestReviewCompletedConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<ManifestReviewCompleted>
{
    public async Task Consume(ConsumeContext<ManifestReviewCompleted> context)
    {
        var message = context.Message;
        // the review was opened first (same publisher, earlier commit); retried until that projection exists
        var server = (await db.Servers.FindAsync([message.ServerId], context.CancellationToken))
            .OrThrowNotFound($"Server {message.ServerId} is not projected yet.");
        if (server.OpenReviewId == message.ReviewId)
        {
            server.OpenReviewId = null;
            server.OpenReviewRisk = null;
        }
        server.LastReviewRisk = message.RiskLevel;

        var entry = db.AddTimeline(context, message.OccurredOn,
            $"Review {message.Decision.ToLowerInvariant()} by {string.Join(", ", message.DecidedBy)} (risk {message.RiskLevel})" +
            (string.IsNullOrWhiteSpace(message.Comment) ? "." : $": {message.Comment}"),
            message.Decision == "Approved" ? Severities.Info : Severities.Warning,
            serverId: message.ServerId, reviewId: message.ReviewId, actorId: message.DecidedBy.LastOrDefault());
        await db.SaveChangesAsync(context.CancellationToken);
        feed.Publish(ActivityItem.From(entry));
    }
}
