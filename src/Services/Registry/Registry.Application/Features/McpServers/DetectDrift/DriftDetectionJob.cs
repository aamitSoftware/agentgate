using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Registry.Application.Features.McpServers.QuarantineServer;

namespace Registry.Application.Features.McpServers.DetectDrift;

public sealed class DriftDetectionOptions
{
    [Range(1, 3600)]
    public int IntervalSeconds { get; init; }
}

/// <summary>
/// Re-reads tools/list from every approved server and quarantines the ones whose manifest no longer
/// matches the pinned hash: the rug-pull detector.
/// </summary>
// ponytail: one sequential sweep per interval in one replica; shard or lease servers if there are thousands or several replicas
public sealed class DriftDetectionJob(IServiceScopeFactory scopeFactory, IOptions<DriftDetectionOptions> options,
    ILogger<DriftDetectionJob> logger) : BackgroundService
{
    public const string ActorId = "system:drift-detector";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await SweepAsync(stoppingToken);
    }

    public async Task SweepAsync(CancellationToken ct)
    {
        List<(Guid Id, string Name, ServerUrl Url, ToolManifest Pinned)> approved;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            approved = (await scope.ServiceProvider.GetRequiredService<RegistryDbContext>().McpServers.AsNoTracking()
                    .Where(server => server.Status == ServerStatus.Approved)
                    .Select(server => new { server.Id, server.Name, server.Url, server.PinnedManifest })
                    .ToListAsync(ct))
                .Select(server => (server.Id, server.Name, server.Url, server.PinnedManifest!))
                .ToList();
        }

        foreach (var server in approved)
        {
            // one scope per server: a failure on one server must not leave tracked state for the next
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                var observed = await scope.ServiceProvider.GetRequiredService<McpToolsClient>().FetchManifestAsync(server.Url, ct);
                if (observed == server.Pinned)
                    continue;

                logger.LogWarning("Manifest drift on {ServerName}: pinned {PinnedHash}, observed {ObservedHash}; quarantining",
                    server.Name, server.Pinned.Hash, observed.Hash);
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new QuarantineServerCommand
                {
                    ServerId = server.Id,
                    ObservedManifest = observed,
                    ActorId = ActorId,
                    Comment = $"Manifest drifted from pinned {server.Pinned.Hash[..12]} to {observed.Hash[..12]}."
                }, ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                // an unreachable server is not evidence of a rug pull: keep it as is and check again next sweep
                logger.LogWarning(exception, "Drift check failed for {ServerName}", server.Name);
            }
        }
    }
}
