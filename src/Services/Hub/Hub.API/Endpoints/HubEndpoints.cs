using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

namespace Hub.API.Endpoints;

public static class ServerEndpoints
{
    public static void Map(this IEndpointRouteBuilder app)
    {
        app.MapGet("/servers", async (ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetServersQuery(), cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("GetServers")
            .WithTags("Inventory");

        app.MapGet("/servers/{serverId:guid}", async (Guid serverId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetServerQuery(serverId), cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("GetServer")
            .WithTags("Inventory");
    }
}

public static class AgentEndpoints
{
    public static void Map(this IEndpointRouteBuilder app)
    {
        app.MapGet("/agents", async (ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetAgentsQuery(), cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("GetAgents")
            .WithTags("Inventory");

        app.MapGet("/agents/{agentId:guid}", async (Guid agentId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetAgentQuery(agentId), cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("GetAgent")
            .WithTags("Inventory");
    }
}

public static class AccessEndpoint
{
    public static void Map(this IEndpointRouteBuilder app) =>
        app.MapGet("/access", async (ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetAccessQuery(), cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("GetAccess")
            .WithTags("Inventory");
}

public static class ActivityEndpoints
{
    private static readonly TimeSpan _heartbeat = TimeSpan.FromSeconds(15);

    public static void Map(this IEndpointRouteBuilder app)
    {
        app.MapGet("/activity", async (int? limit, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetRecentActivityQuery(limit ?? 100), cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("GetRecentActivity")
            .WithTags("Activity");

        // insight - SSE over a plain authenticated GET: the console reads it with fetch (EventSource can't send a bearer token)
        app.MapGet("/activity/stream", (ActivityFeed feed, CancellationToken cancellationToken) =>
                TypedResults.ServerSentEvents(Stream(feed, cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("StreamActivity")
            .WithTags("Activity");
    }

    private static async IAsyncEnumerable<SseItem<ActivityItem?>> Stream(ActivityFeed feed, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in feed.SubscribeAsync(_heartbeat, ct))
            yield return item is null
                ? new SseItem<ActivityItem?>(null, "heartbeat")
                : new SseItem<ActivityItem?>(item, "activity") { EventId = item.Id.ToString() };
    }
}

public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        ServerEndpoints.Map(app);
        AgentEndpoints.Map(app);
        AccessEndpoint.Map(app);
        ActivityEndpoints.Map(app);
        return app;
    }
}
