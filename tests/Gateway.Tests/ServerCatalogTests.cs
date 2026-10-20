using AgentGate.IntegrationEvents.Registry;
using Gateway.API.Features.Catalog;

namespace Gateway.Tests;

public class ServerCatalogTests
{
    private static readonly Guid _serverId = Guid.CreateVersion7();
    private static readonly DateTime _now = DateTime.UtcNow;

    private static ServerApproved Approved(DateTime occurredOn) =>
        new(_serverId, "crm", "http://crm/mcp", "sha256:pinned", GatewayEndToEndTests.PinnedToolsJson, occurredOn);

    private static ServerQuarantined Quarantined(DateTime occurredOn) =>
        new(_serverId, "crm", "sha256:pinned", "sha256:observed", "Manifest drift", occurredOn);

    [Fact]
    public void Approval_pins_the_manifest_tools()
    {
        var catalog = new ServerCatalog();

        catalog.Pin(Approved(_now));

        Assert.True(catalog.TryGet("crm", out var server));
        Assert.False(server.Quarantined);
        Assert.Equal(["export_all_customers", "lookup_customer"], server.Tools.Select(tool => tool.Name));
    }

    [Fact]
    public void A_stale_approval_cannot_lift_a_quarantine()
    {
        var catalog = new ServerCatalog();
        catalog.Pin(Approved(_now));
        catalog.Quarantine(Quarantined(_now.AddSeconds(10)));

        catalog.Pin(Approved(_now)); // redelivered or reordered: older than the quarantine

        Assert.True(catalog.TryGet("crm", out var server));
        Assert.True(server.Quarantined);
    }

    [Fact]
    public void A_quarantine_for_a_server_never_seen_approved_still_blocks_its_older_approval()
    {
        var catalog = new ServerCatalog();

        catalog.Quarantine(Quarantined(_now.AddSeconds(10)));
        catalog.Pin(Approved(_now));

        Assert.True(catalog.TryGet("crm", out var server));
        Assert.True(server.Quarantined);
    }

    [Fact]
    public void A_newer_approval_lifts_the_quarantine()
    {
        var catalog = new ServerCatalog();
        catalog.Quarantine(Quarantined(_now));

        catalog.Pin(Approved(_now.AddSeconds(10)));

        Assert.True(catalog.TryGet("crm", out var server));
        Assert.False(server.Quarantined);
    }

    [Fact]
    public void Redelivering_the_same_event_converges_to_the_same_state()
    {
        var catalog = new ServerCatalog();
        var approved = Approved(_now);

        catalog.Pin(approved);
        catalog.TryGet("crm", out var first);
        catalog.Pin(approved);
        catalog.TryGet("crm", out var second);

        Assert.Equal(first!.AsOf, second!.AsOf);
        Assert.Equal(first.Tools.Count, second.Tools.Count);
        Assert.False(second.Quarantined);
    }
}
