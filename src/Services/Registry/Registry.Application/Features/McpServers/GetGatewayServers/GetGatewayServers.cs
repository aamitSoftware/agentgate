namespace Registry.Application.Features.McpServers.GetGatewayServers;

/// <summary>A server as the gateway enforces it: the pinned manifest and whether it is blocked, as of its last change.</summary>
/// <param name="AsOf">When the server last changed; the gateway orders this against the events it also receives.</param>
public sealed record GatewayServerDto(Guid ServerId, string Name, string Url, string Status, string ManifestHash, string ToolsJson, DateTime AsOf);

/// <summary>Every server with a pinned manifest, for the gateway's startup read. Agents never see the others.</summary>
public sealed record GetGatewayServersQuery : IQuery<IReadOnlyList<GatewayServerDto>>;

// ponytail: unpaged; page by name once there are thousands of servers
public sealed class GetGatewayServersHandler(RegistryDbContext db) : IQueryHandler<GetGatewayServersQuery, IReadOnlyList<GatewayServerDto>>
{
    public async ValueTask<IReadOnlyList<GatewayServerDto>> Handle(GetGatewayServersQuery query, CancellationToken cancellationToken)
    {
        var servers = await db.McpServers.AsNoTracking().Where(server => server.PinnedManifest != null).ToListAsync(cancellationToken);
        return
        [
            .. servers.Select(server => new GatewayServerDto(server.Id, server.Name, server.Url.Value, server.Status.ToString(),
                server.PinnedManifest!.Hash, server.PinnedManifest.ToolsJson, server.LastModifiedOn ?? server.CreatedOn))
        ];
    }
}
