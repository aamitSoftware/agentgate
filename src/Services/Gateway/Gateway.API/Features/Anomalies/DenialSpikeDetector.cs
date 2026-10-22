using System.Collections.Concurrent;
using System.Threading.Channels;
using AgentGate.IntegrationEvents.Gateway;
using MassTransit;
using Microsoft.Extensions.Options;

namespace Gateway.API.Features.Anomalies;

/// <summary>
/// v1's one detector: an agent piling up denied calls is probing for tools it wasn't given, the typical sign of a
/// prompt-injected agent. Crossing the threshold blocks the agent here at once (no round trip to wait for); Review
/// then suspends its grants, and a reviewer resuming one lifts the block.
/// </summary>
// ponytail: in memory, one replica; a restart forgets blocks and counts, which is safe because the grants are
// suspended in Review by then. Several replicas would need the counts in a shared store (Redis), as plan.md says.
public sealed class DenialSpikeDetector(TimeProvider timeProvider, IOptions<GatewayOptions> options)
{
    public const string Kind = "DeniedCallSpike";

    private readonly ConcurrentDictionary<string, AgentWindow> _windows = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _blockedSince = new(StringComparer.Ordinal);

    private TimeSpan Window => TimeSpan.FromSeconds(options.Value.AnomalyWindowSeconds);

    public bool IsBlocked(string agentClientId) => _blockedSince.ContainsKey(agentClientId);

    /// <summary>Counts one denial; returns the anomaly when this one crosses the threshold (once per block).</summary>
    public AnomalyDetected? RecordDenial(string agentClientId, string target)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var window = _windows.GetOrAdd(agentClientId, _ => new AgentWindow());
        lock (window)
        {
            window.Denials.Enqueue((now, target));
            while (window.Denials.TryPeek(out var oldest) && now - oldest.At > Window)
                window.Denials.Dequeue();

            if (window.Denials.Count < options.Value.AnomalyThreshold || !_blockedSince.TryAdd(agentClientId, now))
                return null;

            var targets = window.Denials.Reverse().Select(denial => denial.Target).Distinct().Take(10).ToList();
            var count = window.Denials.Count;
            window.Denials.Clear();
            return new AnomalyDetected(NewId.NextGuid(), agentClientId, Kind, count, options.Value.AnomalyWindowSeconds, targets, now);
        }
    }

    /// <summary>A grant of this agent was (re)activated after the block: a reviewer has looked, so the agent may call again.</summary>
    public void Lift(string agentClientId, DateTime activatedOn)
    {
        if (_blockedSince.TryGetValue(agentClientId, out var since) && activatedOn > since)
            _blockedSince.TryRemove(KeyValuePair.Create(agentClientId, since));
    }

    private sealed class AgentWindow
    {
        public Queue<(DateTime At, string Target)> Denials { get; } = new();
    }
}

/// <summary>Anomalies leave the request path at once and are published with retries: losing one would leave grants active.</summary>
public sealed class AnomalyQueue
{
    private readonly Channel<AnomalyDetected> _channel = Channel.CreateUnbounded<AnomalyDetected>(new UnboundedChannelOptions { SingleReader = true });

    public void Report(AnomalyDetected anomaly) => _channel.Writer.TryWrite(anomaly);

    public IAsyncEnumerable<AnomalyDetected> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed class AnomalyPublisher(AnomalyQueue queue, IBus bus, ILogger<AnomalyPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan _maxDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var anomaly in queue.ReadAllAsync(stoppingToken))
            for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
            {
                try
                {
                    await bus.Publish(anomaly, stoppingToken);
                    break;
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    var delay = TimeSpan.FromSeconds(Math.Min(_maxDelay.TotalSeconds, Math.Pow(2, attempt)));
                    logger.LogError(exception, "Could not publish anomaly {AnomalyId} for {AgentClientId}; retrying in {Delay}",
                        anomaly.AnomalyId, anomaly.AgentClientId, delay);
                    await Task.Delay(delay, stoppingToken);
                }
            }
    }
}
