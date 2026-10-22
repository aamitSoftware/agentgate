using System.Text.Json;
using Hub.Application.Features.Grants;

namespace Hub.Application.Features.Servers;

public sealed record ServerSummaryDto(Guid Id, string Name, string? Url, string Status, DateTime StatusAsOf, string? PinnedHash,
    int ToolCount, string? PendingHash, Guid? OpenReviewId, string? OpenReviewRisk, string? LastReviewRisk, int ActiveGrantCount);

/// <param name="PinnedTools">The approved manifest's tools exactly as agents see them (canonical MCP Tool objects).</param>
public sealed record ServerDetailDto(ServerSummaryDto Summary, JsonElement? PinnedTools, IReadOnlyList<GrantDto> Grants,
    IReadOnlyList<ActivityItem> Timeline, IReadOnlyList<ActivityItem> RecentCalls);

public sealed record GetServersQuery : IQuery<IReadOnlyList<ServerSummaryDto>>;

public sealed record GetServerQuery(Guid ServerId) : IQuery<ServerDetailDto>;

public sealed class GetServersHandler(HubDbContext db) : IQueryHandler<GetServersQuery, IReadOnlyList<ServerSummaryDto>>
{
    public async ValueTask<IReadOnlyList<ServerSummaryDto>> Handle(GetServersQuery query, CancellationToken cancellationToken)
    {
        var activeGrants = await db.Grants.AsNoTracking().Where(grant => grant.Status == GrantDto.Active)
            .GroupBy(grant => grant.ServerId).Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        var servers = await db.Servers.AsNoTracking().OrderBy(server => server.Name).ToListAsync(cancellationToken);
        return [.. servers.Select(server => Summary(server, activeGrants.GetValueOrDefault(server.Id)))];
    }

    internal static ServerSummaryDto Summary(ServerView server, int activeGrantCount) =>
        new(server.Id, server.Name, server.Url, server.Status, server.StatusAsOf, server.PinnedHash, ToolCount(server.PinnedToolsJson),
            server.PendingHash, server.OpenReviewId, server.OpenReviewRisk, server.LastReviewRisk, activeGrantCount);

    private static int ToolCount(string? toolsJson)
    {
        if (toolsJson is null)
            return 0;
        using var document = JsonDocument.Parse(toolsJson);
        return document.RootElement.GetArrayLength();
    }
}

public sealed class GetServerHandler(HubDbContext db) : IQueryHandler<GetServerQuery, ServerDetailDto>
{
    private const int RecentLimit = 100;

    public async ValueTask<ServerDetailDto> Handle(GetServerQuery query, CancellationToken cancellationToken)
    {
        var server = (await db.Servers.AsNoTracking().SingleOrDefaultAsync(server => server.Id == query.ServerId, cancellationToken))
            .OrThrowNotFound($"Server {query.ServerId} not found.");

        var grants = await db.Grants.AsNoTracking().Where(grant => grant.ServerId == server.Id)
            .OrderBy(grant => grant.AgentClientId).ToListAsync(cancellationToken);
        var timeline = await db.Timeline.AsNoTracking().Where(entry => entry.ServerId == server.Id)
            .OrderByDescending(entry => entry.OccurredOn).Take(RecentLimit).ToListAsync(cancellationToken);
        var calls = await db.ToolCalls.AsNoTracking().Where(call => call.ServerName == server.Name)
            .OrderByDescending(call => call.OccurredOn).Take(RecentLimit).ToListAsync(cancellationToken);

        JsonElement? pinnedTools = server.PinnedToolsJson is null ? null : JsonDocument.Parse(server.PinnedToolsJson).RootElement.Clone();
        return new ServerDetailDto(GetServersHandler.Summary(server, grants.Count(grant => grant.Status == GrantDto.Active)), pinnedTools,
            [.. grants.Select(GrantDto.From)], [.. timeline.Select(ActivityItem.From)], [.. calls.Select(ActivityItem.From)]);
    }
}
