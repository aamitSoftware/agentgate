namespace Hub.Application.Features.Activity;

/// <summary>The newest activity, calls and governance events interleaved, for the live screen's first paint.</summary>
public sealed record GetRecentActivityQuery(int Limit) : IQuery<IReadOnlyList<ActivityItem>>;

public sealed class GetRecentActivityHandler(HubDbContext db) : IQueryHandler<GetRecentActivityQuery, IReadOnlyList<ActivityItem>>
{
    public const int MaxLimit = 500;

    public async ValueTask<IReadOnlyList<ActivityItem>> Handle(GetRecentActivityQuery query, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(query.Limit, 1, MaxLimit);
        var calls = await db.ToolCalls.AsNoTracking().OrderByDescending(call => call.OccurredOn).Take(limit).ToListAsync(cancellationToken);
        var events = await db.Timeline.AsNoTracking().OrderByDescending(entry => entry.OccurredOn).Take(limit).ToListAsync(cancellationToken);

        return [.. calls.Select(ActivityItem.From).Concat(events.Select(ActivityItem.From))
            .OrderByDescending(item => item.OccurredOn).Take(limit)];
    }
}
