using System.Diagnostics;
using System.Threading.Channels;
using AgentGate.IntegrationEvents.Gateway;
using MassTransit;

namespace Gateway.API.Features.Observation;

/// <summary>
/// Decouples the proxy path from the broker: the request path only enqueues, a background loop publishes.
/// When the broker is slow and the queue is full the oldest observation is dropped, never the request.
/// </summary>
public sealed class ObservationQueue
{
    private const int Capacity = 10_000;

    private readonly Channel<ToolCallObserved> _channel;
    private long _dropped;

    public ObservationQueue() =>
        _channel = Channel.CreateBounded<ToolCallObserved>(
            new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true },
            _ => Interlocked.Increment(ref _dropped));

    public long Dropped => Interlocked.Read(ref _dropped);

    public void Observe(string agentClientId, string serverName, string method, string? toolName, string? denialReason) =>
        _channel.Writer.TryWrite(new ToolCallObserved(NewId.NextGuid(), agentClientId, serverName, method, toolName,
            denialReason is null ? "Allowed" : "Denied", denialReason, Activity.Current?.TraceId.ToString(),
            DateTime.UtcNow));

    public IAsyncEnumerable<ToolCallObserved> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed class ObservationPublisher(ObservationQueue queue, IBus bus, ILogger<ObservationPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var observation in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await bus.Publish(observation, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // an observation is not a decision: log and move on rather than stall the queue behind a broken broker
                logger.LogWarning(exception, "Could not publish observation {ObservationId}; {Dropped} dropped so far",
                    observation.ObservationId, queue.Dropped);
            }
        }
    }
}
