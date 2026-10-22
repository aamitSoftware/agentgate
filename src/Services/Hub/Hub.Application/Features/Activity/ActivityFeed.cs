using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Hub.Application.Features.Activity;

/// <summary>
/// One row of the live activity screen: a gateway decision (<c>call</c>) or a governance event (<c>event</c>).
/// </summary>
public sealed record ActivityItem(Guid Id, string Kind, DateTime OccurredOn, string Severity, string? AgentClientId, string? ServerName,
    string? Method, string? ToolName, string? Decision, string? Reason, string? TraceId, string? EventKind, string? Summary,
    string? ActorId, Guid? ServerId, Guid? AgentId)
{
    public const string Call = "call";
    public const string Event = "event";

    public static ActivityItem From(ToolCallView call) =>
        new(call.Id, Call, call.OccurredOn, call.Decision == ToolCallDecisions.Denied ? Severities.Warning : Severities.Info,
            call.AgentClientId, call.ServerName, call.Method, call.ToolName, call.Decision, call.Reason, call.TraceId,
            null, null, null, null, null);

    public static ActivityItem From(TimelineEntry entry) =>
        new(entry.Id, Event, entry.OccurredOn, entry.Severity, null, null, null, null, null, null, null,
            entry.Kind, entry.Summary, entry.ActorId, entry.ServerId, entry.AgentId);
}

public static class Severities
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Critical = "critical";
}

public static class ToolCallDecisions
{
    public const string Denied = "Denied";
}

/// <summary>
/// Fans live activity out to every open SSE stream. Each subscriber has its own bounded buffer, so one slow browser
/// loses its oldest rows instead of slowing the consumers that feed it.
/// </summary>
// ponytail: in memory, one Hub replica; several replicas would each see only the events their own queue delivered
public sealed class ActivityFeed
{
    private const int SubscriberBuffer = 500;

    private readonly ConcurrentDictionary<Guid, Channel<ActivityItem>> _subscribers = new();

    public int SubscriberCount => _subscribers.Count;

    public void Publish(ActivityItem item)
    {
        foreach (var subscriber in _subscribers.Values)
            subscriber.Writer.TryWrite(item);
    }

    /// <summary>
    /// Live items until <paramref name="ct"/> fires; null right away (the subscription is registered, so the response can
    /// start) and then every <paramref name="heartbeat"/> with nothing to send.
    /// </summary>
    public async IAsyncEnumerable<ActivityItem?> SubscribeAsync(TimeSpan heartbeat, [EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<ActivityItem>(
            new BoundedChannelOptions(SubscriberBuffer) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        _subscribers[id] = channel;
        try
        {
            yield return null; // a client sees the stream only once something is written: tell it it is live now

            Task<bool>? waitForItems = null;
            while (!ct.IsCancellationRequested)
            {
                waitForItems ??= channel.Reader.WaitToReadAsync(ct).AsTask();
                var first = await Task.WhenAny(waitForItems, Task.Delay(heartbeat, ct));
                if (ct.IsCancellationRequested)
                    yield break; // the client went away: end quietly
                if (first != waitForItems)
                {
                    yield return null; // keeps proxies from closing an idle stream; the same wait continues
                    continue;
                }

                if (!await waitForItems)
                    yield break;
                waitForItems = null;
                while (channel.Reader.TryRead(out var item))
                    yield return item;
            }
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
        }
    }
}
