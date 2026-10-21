using AgentGate.IntegrationEvents.Registry;
using Microsoft.Extensions.Logging;
using Review.Application.Features.ManifestReviews.RiskScanning;

namespace Review.Application.Features.ManifestReviews.OpenReview;

public sealed record OpenReviewCommand : ReviewCommand
{
    public override ReviewActionType ActionType => ReviewActionType.Open;
}

public sealed record SupersedeReviewCommand : ReviewCommand
{
    public override ReviewActionType ActionType => ReviewActionType.Supersede;
}

/// <summary>
/// Registry submitted a manifest (new server, or drift): scan it and open a review. Any undecided review of an
/// older manifest of the same server is superseded - only the newest manifest can be decided.
/// </summary>
public sealed class ManifestSubmittedConsumer(ReviewDbContext db, ManifestReviewRepository reviews, IRiskScanner scanner,
    ReviewStateMachineFactory stateMachineFactory, ILogger<ManifestSubmittedConsumer> logger) : IConsumer<ManifestSubmitted>
{
    public async Task Consume(ConsumeContext<ManifestSubmitted> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        // submissions of one server can arrive out of order; a newer review already open wins
        if (await reviews.Query().AnyAsync(review => review.ServerId == message.ServerId && review.CreatedOn >= message.OccurredOn, ct))
        {
            logger.LogInformation("Ignoring stale manifest {ManifestHash} for {ServerName}: a newer review exists",
                message.ManifestHash, message.Name);
            return;
        }

        if (await db.KnownServers.FindAsync([message.ServerId], ct) is null)
            db.KnownServers.Add(KnownServer.Create(message.ServerId, message.Name)); // names are immutable in Registry

        var comment = $"Superseded by manifest {message.ManifestHash[..12]}.";
        foreach (var undecided in await reviews.GetUndecidedAsync(message.ServerId, ct))
            undecided.Supersede(stateMachineFactory, new SupersedeReviewCommand
            {
                ReviewId = undecided.Id,
                ActorId = SystemActors.ReviewIntake,
                OccurredOn = message.OccurredOn,
                Comment = comment
            });

        var assessment = await scanner.ScanAsync(new ManifestToScan(message.Name, message.ToolsJson, message.PreviousToolsJson), ct);
        var review = ManifestReview.Open(message.ServerId, message.Name, message.ManifestHash, message.ToolsJson,
            message.PreviousHash, message.PreviousToolsJson, message.Reason, assessment,
            new OpenReviewCommand { ActorId = SystemActors.ReviewIntake, OccurredOn = message.OccurredOn, Comment = message.Reason });

        await reviews.AddAsync(review, ct);
        await reviews.SaveChangesAsync(ct);
        logger.LogInformation("Opened review {ReviewId} of {ServerName} manifest {ManifestHash}: risk {RiskLevel}, {FindingCount} findings",
            review.Id, message.Name, message.ManifestHash, review.RiskLevel, review.Findings.Count);
    }
}
