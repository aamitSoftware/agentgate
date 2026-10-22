using System.Net.Http.Json;
using AgentGate.IntegrationEvents.Registry;
using AgentGate.IntegrationEvents.Review;
using Gateway.API.Features.Catalog;
using Gateway.API.Features.Grants;
using Blocks.AspNetCore;
using Microsoft.Extensions.Options;

namespace Gateway.API.Features.Bootstrap;

/// <summary>Registry's view of a server the gateway enforces (GET /internal/gateway/servers).</summary>
public sealed record ServerSnapshotRow(Guid ServerId, string Name, string Url, string Status, string ManifestHash, string ToolsJson, DateTime AsOf);

/// <summary>Review's view of a grant the gateway enforces (GET /internal/gateway/grants).</summary>
public sealed record GrantSnapshotRow(Guid GrantId, string AgentClientId, string ServerName, IReadOnlyList<string> Tools, DateTime? ExpiresAt,
    string Status, DateTime AsOf);

/// <summary>
/// Rebuilds the gateway's local copies after a restart from Registry and Review, then leaves the rest to events.
/// Until it succeeds the gateway keeps failing closed. Each row carries the time of its last change, the same
/// time the matching event carries, so the snapshot and events in flight converge whichever arrives first.
/// </summary>
public sealed class GatewayBootstrap(IHttpClientFactory httpClientFactory, ServerCatalog catalog, GrantTable grants,
    ILogger<GatewayBootstrap> logger) : BackgroundService
{
    public const string RegistryClient = "registry";
    public const string ReviewClient = "review";

    private static readonly TimeSpan _maxDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadWithRetryAsync("servers", async ct =>
        {
            var rows = await httpClientFactory.CreateClient(RegistryClient)
                .GetFromJsonAsync<List<ServerSnapshotRow>>("/internal/gateway/servers", ct);
            return Apply(rows!, catalog);
        }, stoppingToken);

        await LoadWithRetryAsync("grants", async ct =>
        {
            var rows = await httpClientFactory.CreateClient(ReviewClient)
                .GetFromJsonAsync<List<GrantSnapshotRow>>("/internal/gateway/grants", ct);
            return Apply(rows!, grants);
        }, stoppingToken);
    }

    private async Task LoadWithRetryAsync(string what, Func<CancellationToken, Task<int>> load, CancellationToken ct)
    {
        for (var attempt = 1; !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                var count = await load(ct);
                logger.LogInformation("Bootstrapped {Count} {What}", count, what);
                return;
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(_maxDelay.TotalSeconds, Math.Pow(2, attempt)));
                logger.LogWarning(exception, "Could not bootstrap {What} (attempt {Attempt}); retrying in {Delay}", what, attempt, delay);
                await Task.Delay(delay, ct);
            }
        }
    }

    // insight - the snapshot goes through the same overwrite-if-newer methods as the events, so there is one ordering
    // rule, not two: a row older than an event already applied is dropped, and a later stale event is dropped too

    public static int Apply(IReadOnlyList<ServerSnapshotRow> rows, ServerCatalog catalog)
    {
        foreach (var row in rows)
        {
            catalog.Pin(new ServerApproved(row.ServerId, row.Name, row.Url, row.ManifestHash, row.ToolsJson, row.AsOf));
            if (row.Status != "Approved") // Quarantined, or Rejected after a drift: the pinned manifest stays blocked
                catalog.Quarantine(new ServerQuarantined(row.ServerId, row.Name, row.ManifestHash, "", $"{row.Status} (restored at startup)", row.AsOf));
        }
        return rows.Count;
    }

    public static int Apply(IReadOnlyList<GrantSnapshotRow> rows, GrantTable grants)
    {
        foreach (var row in rows)
            switch (row.Status)
            {
                case "Active":
                    grants.Activate(new GrantActivated(row.GrantId, Guid.Empty, row.AgentClientId, Guid.Empty, row.ServerName, row.Tools,
                        row.ExpiresAt, row.AsOf));
                    break;
                case "Suspended":
                    grants.Suspend(new GrantSuspended(row.GrantId, "restored at startup", row.AsOf));
                    break;
                default:
                    grants.Revoke(new GrantRevoked(row.GrantId, "restored at startup", row.AsOf));
                    break;
            }
        return rows.Count;
    }
}

public static class GatewayBootstrapRegistration
{
    /// <summary>The startup read, authenticated as the gateway's own service identity (role gateway-sync).</summary>
    public static IHostApplicationBuilder AddGatewayBootstrap(this IHostApplicationBuilder builder)
    {
        builder.AddServiceIdentity();
        builder.Services.AddHttpClient(GatewayBootstrap.RegistryClient, (serviceProvider, client) =>
                client.BaseAddress = new Uri(serviceProvider.GetRequiredService<IOptions<GatewayOptions>>().Value.RegistryAddress))
            .AddHttpMessageHandler<ServiceTokenHandler>();
        builder.Services.AddHttpClient(GatewayBootstrap.ReviewClient, (serviceProvider, client) =>
                client.BaseAddress = new Uri(serviceProvider.GetRequiredService<IOptions<GatewayOptions>>().Value.ReviewAddress))
            .AddHttpMessageHandler<ServiceTokenHandler>();
        builder.Services.AddHostedService<GatewayBootstrap>();
        return builder;
    }
}
