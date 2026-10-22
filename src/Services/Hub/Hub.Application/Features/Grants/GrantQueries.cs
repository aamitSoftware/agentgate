namespace Hub.Application.Features.Grants;

public sealed record GrantDto(Guid Id, Guid AgentId, string AgentClientId, Guid ServerId, string ServerName, IReadOnlyList<string> Tools,
    DateTime? ExpiresAt, string Status, DateTime StatusAsOf)
{
    public const string Active = "Active";

    public static GrantDto From(GrantView grant) =>
        new(grant.Id, grant.AgentId, grant.AgentClientId, grant.ServerId, grant.ServerName, grant.Tools, grant.ExpiresAt, grant.Status,
            grant.StatusAsOf);
}

/// <summary>Who can call what right now: active, unexpired grants.</summary>
public sealed record GetAccessQuery : IQuery<IReadOnlyList<GrantDto>>;

public sealed class GetAccessHandler(HubDbContext db, TimeProvider timeProvider) : IQueryHandler<GetAccessQuery, IReadOnlyList<GrantDto>>
{
    public async ValueTask<IReadOnlyList<GrantDto>> Handle(GetAccessQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var grants = await db.Grants.AsNoTracking()
            .Where(grant => grant.Status == GrantDto.Active && (grant.ExpiresAt == null || grant.ExpiresAt > now))
            .OrderBy(grant => grant.AgentClientId).ThenBy(grant => grant.ServerName)
            .ToListAsync(cancellationToken);
        return [.. grants.Select(GrantDto.From)];
    }
}
