using AgentGate.IntegrationEvents.Review;
using Gateway.API.Features.Grants;

namespace Gateway.Tests;

public class GrantTableTests
{
    private static readonly Guid _grantId = Guid.CreateVersion7();
    private static readonly DateTime _now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly FixedTime _time = new(_now);

    private static GrantActivated Activated(DateTime occurredOn, DateTime? expiresAt = null) =>
        new(_grantId, Guid.CreateVersion7(), "demo-agent", Guid.CreateVersion7(), "crm", ["lookup_customer"], expiresAt, occurredOn);

    [Fact]
    public void Deny_by_default()
    {
        var grants = new GrantTable(_time);

        Assert.False(grants.IsGranted("demo-agent", "crm", "lookup_customer"));
    }

    [Fact]
    public void An_active_grant_allows_exactly_its_agent_server_and_tools()
    {
        var grants = new GrantTable(_time);

        grants.Activate(Activated(_now));

        Assert.True(grants.IsGranted("demo-agent", "crm", "lookup_customer"));
        Assert.False(grants.IsGranted("demo-agent", "crm", "export_all_customers"));
        Assert.False(grants.IsGranted("other-agent", "crm", "lookup_customer"));
        Assert.False(grants.IsGranted("demo-agent", "billing", "lookup_customer"));
    }

    [Fact]
    public void Expiry_is_enforced_at_use_without_any_event()
    {
        var grants = new GrantTable(_time);
        grants.Activate(Activated(_now, expiresAt: _now.AddHours(1)));
        Assert.True(grants.IsGranted("demo-agent", "crm", "lookup_customer"));

        _time.Now = _now.AddHours(1);

        Assert.False(grants.IsGranted("demo-agent", "crm", "lookup_customer"));
    }

    [Fact]
    public void Suspension_blocks_and_a_later_resume_restores()
    {
        var grants = new GrantTable(_time);
        grants.Activate(Activated(_now));

        grants.Suspend(new GrantSuspended(_grantId, "spike", _now.AddSeconds(1)));
        Assert.False(grants.IsGranted("demo-agent", "crm", "lookup_customer"));

        grants.Activate(Activated(_now.AddSeconds(2))); // resume re-publishes GrantActivated
        Assert.True(grants.IsGranted("demo-agent", "crm", "lookup_customer"));
    }

    [Fact]
    public void A_stale_activation_cannot_lift_a_revocation_even_one_seen_first()
    {
        var grants = new GrantTable(_time);

        grants.Revoke(new GrantRevoked(_grantId, "ended", _now.AddSeconds(5))); // arrives before the activation it follows
        grants.Activate(Activated(_now));

        Assert.False(grants.IsGranted("demo-agent", "crm", "lookup_customer"));
    }

    [Fact]
    public void A_redelivered_activation_converges_to_the_same_state()
    {
        var grants = new GrantTable(_time);
        var activated = Activated(_now);

        grants.Activate(activated);
        grants.Activate(activated);

        Assert.True(grants.IsGranted("demo-agent", "crm", "lookup_customer"));
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
