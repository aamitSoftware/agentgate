using Gateway.API;
using Gateway.API.Features.Anomalies;
using Microsoft.Extensions.Options;

namespace Gateway.Tests;

public class DenialSpikeDetectorTests
{
    private static readonly DateTime _start = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly FixedTime _time = new(_start);
    private readonly DenialSpikeDetector _detector;

    public DenialSpikeDetectorTests() =>
        _detector = new DenialSpikeDetector(_time, Options.Create(new GatewayOptions
        {
            Audience = "agentgate-gateway",
            RegistryAddress = "http://registry",
            ReviewAddress = "http://review",
            AnomalyThreshold = 3,
            AnomalyWindowSeconds = 60
        }));

    [Fact]
    public void The_threshold_within_the_window_blocks_the_agent_once()
    {
        Assert.Null(_detector.RecordDenial("agent", "crm/export_all_customers"));
        Assert.Null(_detector.RecordDenial("agent", "crm/delete_customer"));

        var anomaly = _detector.RecordDenial("agent", "crm/export_all_customers");

        Assert.NotNull(anomaly);
        Assert.Equal(("agent", 3, "DeniedCallSpike"), (anomaly.AgentClientId, anomaly.DeniedCount, anomaly.Kind));
        Assert.Equal(["crm/export_all_customers", "crm/delete_customer"], anomaly.RecentTargets);
        Assert.True(_detector.IsBlocked("agent"));
        Assert.False(_detector.IsBlocked("other-agent"));

        foreach (var _ in Enumerable.Range(0, 5))
            Assert.Null(_detector.RecordDenial("agent", "crm/x")); // one anomaly per block, not one per denial
    }

    [Fact]
    public void Denials_spread_beyond_the_window_do_not_add_up()
    {
        _detector.RecordDenial("agent", "crm/a");
        _detector.RecordDenial("agent", "crm/b");
        _time.Now = _start.AddSeconds(61);

        Assert.Null(_detector.RecordDenial("agent", "crm/c"));
        Assert.False(_detector.IsBlocked("agent"));
    }

    [Fact]
    public void Only_an_activation_after_the_block_lifts_it()
    {
        foreach (var target in new[] { "crm/a", "crm/b", "crm/c" })
            _detector.RecordDenial("agent", target);

        _detector.Lift("agent", _start.AddMinutes(-5)); // the original activation, delivered late
        Assert.True(_detector.IsBlocked("agent"));

        _detector.Lift("agent", _start.AddMinutes(1)); // a reviewer resumed a grant
        Assert.False(_detector.IsBlocked("agent"));
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
