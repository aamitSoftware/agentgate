using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Gateway.API.Features.Catalog;
using Yarp.ReverseProxy.Forwarder;

namespace Gateway.API.Features.McpProxy;

public static class McpProxyEndpoint
{
    private const int MaxBodyBytes = 256 * 1024;
    private const int PolicyDenied = -32003; // implementation-defined server error range (-32000..-32099)
    private const string AgentClaim = "azp";  // client-credentials tokens identify the agent by its client id

    // tool calls can legitimately stream for minutes; YARP's default activity timeout is 100 s
    private static readonly ForwarderRequestConfig _requestConfig = new() { ActivityTimeout = TimeSpan.FromMinutes(5) };
    private static readonly HttpTransformer _transformer = new UpstreamTransformer();

    public static IEndpointRouteBuilder MapMcpProxy(this IEndpointRouteBuilder app)
    {
        app.MapPost("/mcp/{serverId}", HandleAsync)
            .RequireAuthorization(policy => policy.RequireClaim(AgentClaim));
        return app;
    }

    private static async Task HandleAsync(string serverId, HttpContext httpContext, ServerCatalog catalog,
        GrantTable grants, IHttpForwarder forwarder, HttpMessageInvoker upstreamClient, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(McpProxyEndpoint));

        var body = await ReadBodyAsync(httpContext.Request, httpContext.RequestAborted);
        if (body is null)
        {
            await WriteErrorAsync(httpContext, new(StatusCodes.Status413PayloadTooLarge, McpRequestInspector.InvalidRequest, "Request body too large."));
            return;
        }

        if (!McpRequestInspector.TryInspect(httpContext.Request.Headers, body, out var request, out var rejection))
        {
            await WriteErrorAsync(httpContext, rejection);
            return;
        }

        var agentId = httpContext.User.FindFirstValue(AgentClaim)!;
        Activity.Current?.SetTag("agentgate.agent", agentId)
            .SetTag("agentgate.server", serverId)
            .SetTag("mcp.method", request.Method)
            .SetTag("mcp.name", request.Name);

        // fail closed: a server the gateway hasn't seen approved does not exist for agents
        if (!catalog.TryGet(serverId, out var server))
        {
            await WriteErrorAsync(httpContext, new(StatusCodes.Status404NotFound, PolicyDenied, $"Unknown MCP server '{serverId}'.", request.Id));
            return;
        }

        // deny by default: only these methods exist for agents in v1
        var denialReason = server.Quarantined
            ? $"MCP server '{serverId}' is quarantined pending re-review."
            : request.Method switch
            {
                "server/discover" or "tools/list" => null,
                "tools/call" when !server.Tools.Any(tool => tool.Name == request.Name) => $"Tool '{request.Name}' is not in the approved manifest.",
                "tools/call" => grants.IsGranted(agentId, serverId, request.Name!) ? null : $"Tool '{request.Name}' is not granted to this agent.",
                _ => $"Method '{request.Method}' is not permitted by gateway policy."
            };

        Activity.Current?.SetTag("agentgate.decision", denialReason is null ? "allow" : "deny")
            .SetTag("agentgate.manifest_hash", server.ManifestHash);
        if (denialReason is not null)
        {
            logger.LogWarning("Denied {Method} {Name} on {ServerId} for agent {AgentId}: {Reason}",
                request.Method, request.Name, serverId, agentId, denialReason);
            await WriteErrorAsync(httpContext, new(StatusCodes.Status403Forbidden, PolicyDenied, denialReason, request.Id));
            return;
        }

        // insight - answered from the pinned manifest, never forwarded: the agent only ever sees tool
        // descriptions that were approved, so a rug pull is prevented at the source, not just detected later
        if (request.Method == "tools/list")
        {
            var tools = server.Tools.Where(tool => grants.IsGranted(agentId, serverId, tool.Name)).Select(tool => tool.Definition).ToList();
            await Results.Json(new ToolsListResponse(request.Id, new ToolsListResult(tools))).ExecuteAsync(httpContext);
            return;
        }

        httpContext.Request.Body = new MemoryStream(body); // the inspected bytes are exactly what goes upstream
        var error = await forwarder.SendAsync(httpContext, server.Url, upstreamClient, _requestConfig, _transformer);
        if (error != ForwarderError.None)
            logger.LogWarning("Forwarding {Method} to {ServerId} failed: {Error}", request.Method, serverId, error);
    }

    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength > MaxBodyBytes)
            return null;

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static Task WriteErrorAsync(HttpContext httpContext, McpRejection rejection) =>
        Results.Json(new JsonRpcErrorResponse(rejection.Id, new(rejection.ErrorCode, rejection.Message)), statusCode: rejection.StatusCode)
            .ExecuteAsync(httpContext);

    private sealed record JsonRpcErrorResponse(JsonElement? Id, JsonRpcError Error)
    {
        public string Jsonrpc => "2.0";
    }

    private sealed record JsonRpcError(int Code, string Message);

    private sealed record ToolsListResponse(JsonElement? Id, ToolsListResult Result)
    {
        public string Jsonrpc => "2.0";
    }

    private sealed record ToolsListResult(IReadOnlyList<JsonElement> Tools);

    private sealed class UpstreamTransformer : HttpTransformer
    {
        public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest,
            string destinationPrefix, CancellationToken cancellationToken)
        {
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

            // the approved URL is the upstream MCP endpoint itself, not a prefix for the gateway's own path
            proxyRequest.RequestUri = new Uri(destinationPrefix);
            // MCP servers must not accept tokens issued for someone else: the agent's token stops here
            proxyRequest.Headers.Authorization = null;
        }
    }
}
