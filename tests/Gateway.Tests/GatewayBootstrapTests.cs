using AgentGate.IntegrationEvents.Registry;
using AgentGate.IntegrationEvents.Review;
using Gateway.API.Features.Bootstrap;
using Gateway.API.Features.Catalog;
using Gateway.API.Features.Grants;

namespace Gateway.Tests;

/// <summary>A restarted gateway rebuilds from snapshots, and snapshots and events agree whichever arrives first.</summary>
public class GatewayBootstrapTests
{
    private static readonly DateTime _now = DateTime.UtcNow;
    private static readonly Guid _serverId = Guid.CreateVersion7();
    private static readonly Guid _grantId = Guid.CreateVersion7();

    private static ServerSnapshotRow Server(string status, DateTime asOf) =>
        new(_serverId, "crm", "http://crm/mcp", status, "sha256:pinned", GatewayEndToEndTests.PinnedToolsJson, asOf);

    private static GrantSnapshotRow Grant(string status, DateTime asOf) =>
        new(_grantId, "demo-agent", "crm", ["lookup_customer"], null, status, asOf);

    [Fact]
    public void Approved_servers_are_served_and_blocked_ones_stay_blocked()
    {
        var approved = new ServerCatalog();
        GatewayBootstrap.Apply([Server("Approved", _now)], approved);
        Assert.True(approved.TryGet("crm", out var served));
        Assert.False(served.Quarantined);
        Assert.Equal(2, served.Tools.Count);

        foreach (var status in new[] { "Quarantined", "Rejected" })
        {
            var blocked = new ServerCatalog();
            GatewayBootstrap.Apply([Server(status, _now)], blocked);
            Assert.True(blocked.TryGet("crm", out var server));
            Assert.True(server.Quarantined, status);
        }
    }

    [Fact]
    public void A_snapshot_older_than_an_event_already_applied_is_ignored()
    {
        var catalog = new ServerCatalog();
        catalog.Quarantine(new ServerQuarantined(_serverId, "crm", "sha256:pinned", "sha256:observed", "drift", _now));

        GatewayBootstrap.Apply([Server("Approved", _now.AddSeconds(-30))], catalog); // read before the drift was committed

        Assert.True(catalog.TryGet("crm", out var server));
        Assert.True(server.Quarantined);
    }

    [Fact]
    public void Grants_come_back_in_force_and_blocks_hold_against_stale_activations()
    {
        var active = new GrantTable(TimeProvider.System);
        GatewayBootstrap.Apply([Grant("Active", _now)], active);
        Assert.True(active.IsGranted("demo-agent", "crm", "lookup_customer"));

        var revoked = new GrantTable(TimeProvider.System);
        GatewayBootstrap.Apply([Grant("Revoked", _now)], revoked);
        revoked.Activate(new GrantActivated(_grantId, Guid.Empty, "demo-agent", _serverId, "crm", ["lookup_customer"], null, _now.AddMinutes(-5)));
        Assert.False(revoked.IsGranted("demo-agent", "crm", "lookup_customer"));
    }

    [Fact]
    public void A_resume_after_the_snapshot_restores_a_suspended_grant()
    {
        var grants = new GrantTable(TimeProvider.System);
        GatewayBootstrap.Apply([Grant("Suspended", _now)], grants);
        Assert.False(grants.IsGranted("demo-agent", "crm", "lookup_customer"));

        grants.Activate(new GrantActivated(_grantId, Guid.Empty, "demo-agent", _serverId, "crm", ["lookup_customer"], null, _now.AddSeconds(5)));

        Assert.True(grants.IsGranted("demo-agent", "crm", "lookup_customer"));
    }
}
