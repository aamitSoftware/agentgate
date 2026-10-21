using Review.Application.Features.ManifestReviews.GetReviews;

namespace Review.Application.Features.Grants.GetGrants;

public sealed record GrantDto(Guid Id, Guid AgentId, string AgentClientId, Guid ServerId, string ServerName, IReadOnlyList<string> Tools,
    DateTime? ExpiresAt, string Justification, GrantStatus Status, string? FirstApproverId, DateTime CreatedOn,
    IReadOnlyList<AuditEntryDto> AuditEntries);

public sealed record GetGrantsQuery(GrantStatus? Status, Guid? AgentId, Guid? ServerId) : IQuery<IReadOnlyList<GrantDto>>;

public sealed record GetGrantQuery(Guid GrantId) : IQuery<GrantDto>;

public sealed class GetGrantsHandler(GrantRepository grants) : IQueryHandler<GetGrantsQuery, IReadOnlyList<GrantDto>>
{
    public async ValueTask<IReadOnlyList<GrantDto>> Handle(GetGrantsQuery query, CancellationToken cancellationToken)
    {
        var matching = grants.Query().AsNoTracking();
        if (query.Status is { } status)
            matching = matching.Where(grant => grant.Status == status);
        if (query.AgentId is { } agentId)
            matching = matching.Where(grant => grant.AgentId == agentId);
        if (query.ServerId is { } serverId)
            matching = matching.Where(grant => grant.ServerId == serverId);

        return (await matching.OrderByDescending(grant => grant.CreatedOn).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    internal static GrantDto ToDto(Grant grant) =>
        new(grant.Id, grant.AgentId, grant.AgentClientId, grant.ServerId, grant.ServerName, grant.Tools, grant.ExpiresAt, grant.Justification,
            grant.Status, grant.FirstApproverId, grant.CreatedOn,
            [.. grant.AuditEntries.OrderBy(entry => entry.OccurredOn).Select(entry => new AuditEntryDto(entry.Action, entry.ActorId,
                entry.OccurredOn, entry.Comment, entry.FromStatus?.ToString(), entry.ToStatus.ToString()))]);
}

public sealed class GetGrantHandler(GrantRepository grants) : IQueryHandler<GetGrantQuery, GrantDto>
{
    public async ValueTask<GrantDto> Handle(GetGrantQuery query, CancellationToken cancellationToken) =>
        GetGrantsHandler.ToDto((await grants.Query().AsNoTracking().SingleOrDefaultAsync(grant => grant.Id == query.GrantId, cancellationToken))
            .OrThrowNotFound($"Grant {query.GrantId} not found."));
}
