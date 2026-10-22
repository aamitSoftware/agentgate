using Hub.Application.Features.Grants;

namespace Hub.Application.Features.Agents;

public sealed record AgentSummaryDto(Guid Id, string Name, string ClientId, string Owner, string Purpose, DateTime RegisteredOn,
    int ActiveGrantCount, int DeniedLastHour);

public sealed record AgentDetailDto(AgentSummaryDto Summary, IReadOnlyList<GrantDto> Grants, IReadOnlyList<ActivityItem> Timeline,
    IReadOnlyList<ActivityItem> RecentCalls);

public sealed record GetAgentsQuery : IQuery<IReadOnlyList<AgentSummaryDto>>;

public sealed record GetAgentQuery(Guid AgentId) : IQuery<AgentDetailDto>;

public sealed class GetAgentsHandler(HubDbContext db, TimeProvider timeProvider) : IQueryHandler<GetAgentsQuery, IReadOnlyList<AgentSummaryDto>>
{
    public async ValueTask<IReadOnlyList<AgentSummaryDto>> Handle(GetAgentsQuery query, CancellationToken cancellationToken)
    {
        var activeGrants = await db.Grants.AsNoTracking().Where(grant => grant.Status == GrantDto.Active)
            .GroupBy(grant => grant.AgentId).Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        var since = timeProvider.GetUtcNow().UtcDateTime.AddHours(-1);
        var denied = await db.ToolCalls.AsNoTracking().Where(call => call.Decision == ToolCallDecisions.Denied && call.OccurredOn >= since)
            .GroupBy(call => call.AgentClientId).Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        var agents = await db.Agents.AsNoTracking().OrderBy(agent => agent.Name).ToListAsync(cancellationToken);
        return [.. agents.Select(agent => Summary(agent, activeGrants.GetValueOrDefault(agent.Id), denied.GetValueOrDefault(agent.ClientId)))];
    }

    internal static AgentSummaryDto Summary(AgentView agent, int activeGrantCount, int deniedLastHour) =>
        new(agent.Id, agent.Name, agent.ClientId, agent.Owner, agent.Purpose, agent.RegisteredOn, activeGrantCount, deniedLastHour);
}

public sealed class GetAgentHandler(HubDbContext db, TimeProvider timeProvider) : IQueryHandler<GetAgentQuery, AgentDetailDto>
{
    private const int RecentLimit = 100;

    public async ValueTask<AgentDetailDto> Handle(GetAgentQuery query, CancellationToken cancellationToken)
    {
        var agent = (await db.Agents.AsNoTracking().SingleOrDefaultAsync(agent => agent.Id == query.AgentId, cancellationToken))
            .OrThrowNotFound($"Agent {query.AgentId} not found.");

        var grants = await db.Grants.AsNoTracking().Where(grant => grant.AgentId == agent.Id)
            .OrderBy(grant => grant.ServerName).ToListAsync(cancellationToken);
        var timeline = await db.Timeline.AsNoTracking().Where(entry => entry.AgentId == agent.Id)
            .OrderByDescending(entry => entry.OccurredOn).Take(RecentLimit).ToListAsync(cancellationToken);
        var calls = await db.ToolCalls.AsNoTracking().Where(call => call.AgentClientId == agent.ClientId)
            .OrderByDescending(call => call.OccurredOn).Take(RecentLimit).ToListAsync(cancellationToken);

        var since = timeProvider.GetUtcNow().UtcDateTime.AddHours(-1);
        var summary = GetAgentsHandler.Summary(agent, grants.Count(grant => grant.Status == GrantDto.Active),
            calls.Count(call => call.Decision == ToolCallDecisions.Denied && call.OccurredOn >= since));
        return new AgentDetailDto(summary, [.. grants.Select(GrantDto.From)], [.. timeline.Select(ActivityItem.From)],
            [.. calls.Select(ActivityItem.From)]);
    }
}
