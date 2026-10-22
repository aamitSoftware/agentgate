using Hub.Application.Features.Activity;

namespace Hub.Tests;

public class ActivityFeedTests
{
    private static ActivityItem Item(string summary) =>
        new(Guid.CreateVersion7(), ActivityItem.Event, DateTime.UtcNow, Severities.Info, null, null, null, null, null, null, null,
            "Test", summary, null, null, null);

    [Fact]
    public async Task Every_subscriber_gets_what_is_published_after_it_subscribed()
    {
        var feed = new ActivityFeed();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var first = feed.SubscribeAsync(TimeSpan.FromMinutes(1), stop.Token).GetAsyncEnumerator(stop.Token);
        await using var second = feed.SubscribeAsync(TimeSpan.FromMinutes(1), stop.Token).GetAsyncEnumerator(stop.Token);
        Assert.True(await first.MoveNextAsync());  // the opening heartbeat: registered and live
        Assert.True(await second.MoveNextAsync());
        Assert.Equal(2, feed.SubscriberCount);

        feed.Publish(Item("quarantined"));

        Assert.True(await first.MoveNextAsync());
        Assert.True(await second.MoveNextAsync());
        Assert.Equal("quarantined", first.Current?.Summary);
        Assert.Equal("quarantined", second.Current?.Summary);
    }

    [Fact]
    public async Task An_idle_stream_gets_a_heartbeat_and_keeps_waiting()
    {
        var feed = new ActivityFeed();
        await using var stream = feed.SubscribeAsync(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await stream.MoveNextAsync());
        Assert.Null(stream.Current);   // opening heartbeat
        Assert.True(await stream.MoveNextAsync());
        Assert.Null(stream.Current);   // idle heartbeat
        Assert.True(await stream.MoveNextAsync());
        Assert.Null(stream.Current);   // and another: the wait carried over

        feed.Publish(Item("live"));
        while (await stream.MoveNextAsync() && stream.Current is null) { }
        Assert.Equal("live", stream.Current?.Summary);
    }

    [Fact]
    public async Task A_closed_stream_unsubscribes()
    {
        var feed = new ActivityFeed();
        using var stop = new CancellationTokenSource();
        var stream = feed.SubscribeAsync(TimeSpan.FromMinutes(1), stop.Token).GetAsyncEnumerator(stop.Token);
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal(1, feed.SubscriberCount);
        var move = stream.MoveNextAsync();

        await stop.CancelAsync();
        Assert.False(await move); // the stream ends quietly
        await stream.DisposeAsync();

        Assert.Equal(0, feed.SubscriberCount);
    }
}
