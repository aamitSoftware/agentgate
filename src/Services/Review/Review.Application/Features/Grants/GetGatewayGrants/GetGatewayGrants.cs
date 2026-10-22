namespace Review.Application.Features.Grants.GetGatewayGrants;

/// <summary>A grant as the gateway enforces it, as of its last change.</summary>
/// <param name="Status">Active, Suspended or Revoked: the gateway keeps the last two as blocks, so an older activation can't lift them.</param>
public sealed record GatewayGrantDto(Guid GrantId, string AgentClientId, string ServerName, IReadOnlyList<string> Tools, DateTime? ExpiresAt,
    string Status, DateTime AsOf);

/// <summary>Every grant that has been in force, for the gateway's startup read.</summary>
public sealed record GetGatewayGrantsQuery : IQuery<IReadOnlyList<GatewayGrantDto>>;

// ponytail: unpaged and keeps revoked grants forever; drop revoked ones older than the longest event delay once that matters
public sealed class GetGatewayGrantsHandler(ReviewDbContext db) : IQueryHandler<GetGatewayGrantsQuery, IReadOnlyList<GatewayGrantDto>>
{
    private static readonly GrantStatus[] _enforced = [GrantStatus.Active, GrantStatus.Suspended, GrantStatus.Revoked];

    public async ValueTask<IReadOnlyList<GatewayGrantDto>> Handle(GetGatewayGrantsQuery query, CancellationToken cancellationToken)
    {
        var grants = await db.Grants.AsNoTracking().Where(grant => _enforced.Contains(grant.Status)).ToListAsync(cancellationToken);
        return
        [
            .. grants.Select(grant => new GatewayGrantDto(grant.Id, grant.AgentClientId, grant.ServerName, grant.Tools, grant.ExpiresAt,
                grant.Status.ToString(), grant.LastModifiedOn ?? grant.CreatedOn))
        ];
    }
}
