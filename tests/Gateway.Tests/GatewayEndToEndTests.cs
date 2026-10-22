using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AgentGate.IntegrationEvents.Gateway;
using AgentGate.IntegrationEvents.Registry;
using AgentGate.IntegrationEvents.Review;
using DemoMcpServer;
using Gateway.API;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Gateway.Tests;

/// <summary>
/// The real MCP C# client → the real gateway → the real demo MCP server, all in memory.
/// Only Keycloak is replaced (by a header-driven test scheme), so this runs without Docker.
/// </summary>
public sealed class GatewayEndToEndTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<CrmTools> _mcpServer = new();
    private WebApplicationFactory<GatewayOptions> _gateway = null!;

    /// <summary>What Registry approved for 'crm': the descriptions the agent must keep seeing whatever the server does later.</summary>
    internal const string PinnedToolsJson = """
        [{"description":"Export every customer record, including contact details.","inputSchema":{"properties":{},"type":"object"},"name":"export_all_customers"},
         {"description":"Look up a single customer by id.","inputSchema":{"properties":{"customerId":{"description":"The customer id, e.g. C-1001.","type":"string"}},"required":["customerId"],"type":"object"},"name":"lookup_customer"}]
        """;

    private static readonly Guid _crmServerId = Guid.CreateVersion7();
    private static readonly Guid _grantId = Guid.CreateVersion7();

    public async ValueTask InitializeAsync()
    {
        _gateway = new WebApplicationFactory<GatewayOptions>().WithWebHostBuilder(web =>
        {
            web.UseSetting("ConnectionStrings:rabbitmq", "amqp://localhost"); // never dialled: the harness swaps in an in-memory bus
            web.ConfigureTestServices(services =>
            {
                services.AddMassTransitTestHarness();
                services.AddSingleton(new HttpMessageInvoker(_mcpServer.Server.CreateHandler()));
                services.AddAuthentication(TestAgentAuthentication.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAgentAuthentication>(TestAgentAuthentication.SchemeName, null);
            });
        });

        // the gateway knows servers only from Registry's events, and grants only from Review's
        await PublishAsync(new ServerApproved(_crmServerId, "crm", "http://demo-mcp-server/mcp", "sha256:pinned", PinnedToolsJson, DateTime.UtcNow));
        await PublishAsync(new GrantActivated(_grantId, Guid.CreateVersion7(), "demo-agent", _crmServerId, "crm", ["lookup_customer"],
            ExpiresAt: null, DateTime.UtcNow));
    }

    [Fact]
    public async Task A_suspended_grant_denies_the_call_it_allowed()
    {
        using (var allowed = await PostAsync("demo-agent", "tools/call", "lookup_customer"))
            Assert.NotEqual(HttpStatusCode.Forbidden, allowed.StatusCode);

        await PublishAsync(new GrantSuspended(_grantId, "Denied-call spike.", DateTime.UtcNow));

        using var denied = await PostAsync("demo-agent", "tools/call", "lookup_customer");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("not granted", await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Every_decision_is_published_as_an_observation()
    {
        using (await PostAsync("demo-agent", "tools/call", "export_all_customers")) { }

        var harness = _gateway.Services.GetTestHarness();
        var observed = await harness.Published.SelectAsync<ToolCallObserved>(
                message => message.Context.Message.ToolName == "export_all_customers", TestContext.Current.CancellationToken)
            .FirstOrDefault();
        Assert.NotNull(observed);
        Assert.Equal(("demo-agent", "crm", "Denied"), (observed.Context.Message.AgentClientId, observed.Context.Message.ServerName,
            observed.Context.Message.Decision));
        Assert.Contains("not granted", observed.Context.Message.Reason);
    }

    [Fact]
    public async Task Another_agent_gets_nothing_from_this_agents_grant()
    {
        using var response = await PostAsync("other-agent", "tools/call", "lookup_customer");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tools_list_is_answered_from_the_pinned_manifest_filtered_to_grants_even_after_a_rug_pull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var rugPull = await _mcpServer.CreateClient().PostAsync("/admin/rug-pull", null, ct);
        rugPull.EnsureSuccessStatusCode();

        await using var client = await ConnectAsAsync("demo-agent");
        var tools = await client.ListToolsAsync(cancellationToken: ct);

        var tool = Assert.Single(tools); // export_all_customers is pinned but not granted
        Assert.Equal("lookup_customer", tool.Name);
        Assert.Equal("Look up a single customer by id.", tool.Description);
    }

    [Fact]
    public async Task Quarantined_server_is_a_403()
    {
        await PublishAsync(new ServerQuarantined(_crmServerId, "crm", "sha256:pinned", "sha256:observed", "Manifest drift", DateTime.UtcNow));

        using var response = await PostAsync("demo-agent", "tools/call", "lookup_customer");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("quarantined", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unknown_server_is_a_404()
    {
        using var response = await PostAsync("demo-agent", "tools/call", "lookup_customer", serverId: "never-approved");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task PublishAsync<T>(T message) where T : class
    {
        var harness = _gateway.Services.GetTestHarness();
        var messageId = NewId.NextGuid();
        await harness.Bus.Publish(message, context => context.MessageId = messageId, TestContext.Current.CancellationToken);
        Assert.True(await harness.Consumed.Any<T>(received => received.Context.MessageId == messageId, TestContext.Current.CancellationToken),
            $"{typeof(T).Name} was not consumed");
    }

    [Fact]
    public async Task Granted_tool_call_reaches_the_server_and_ungranted_one_is_denied()
    {
        await using var client = await ConnectAsAsync("demo-agent");
        var ct = TestContext.Current.CancellationToken;

        var tools = await client.ListToolsAsync(cancellationToken: ct);
        Assert.Contains(tools, tool => tool.Name == "lookup_customer");

        var allowed = await client.CallToolAsync("lookup_customer", new Dictionary<string, object?> { ["customerId"] = "C-1001" }, cancellationToken: ct);
        Assert.Contains("C-1001", allowed.Content.OfType<TextContentBlock>().Single().Text);

        var denied = await Record.ExceptionAsync(() =>
            client.CallToolAsync("export_all_customers", new Dictionary<string, object?>(), cancellationToken: ct).AsTask());
        Assert.True(denied is HttpRequestException { StatusCode: HttpStatusCode.Forbidden } || denied?.Message.Contains("not granted") == true,
            $"Expected the gateway's 403 denial, got: {denied}");
    }

    [Fact]
    public async Task Ungranted_tool_is_a_403_with_a_json_rpc_error()
    {
        using var response = await PostAsync("demo-agent", "tools/call", "export_all_customers");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(-32003, await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Methods_outside_the_allow_list_are_denied()
    {
        using var response = await PostAsync("demo-agent", "resources/list");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Header_smuggling_is_rejected_before_policy()
    {
        using var response = await PostAsync("demo-agent", "tools/call", bodyName: "export_all_customers", headerName: "lookup_customer");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(-32020, await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        using var response = await PostAsync(agentId: null, "tools/call", "lookup_customer");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<McpClient> ConnectAsAsync(string agentId)
    {
        var http = _gateway.CreateClient();
        http.DefaultRequestHeaders.Add(TestAgentAuthentication.AgentHeader, agentId);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp/crm"), TransportMode = HttpTransportMode.StreamableHttp },
            http, ownsHttpClient: true);
        return await McpClient.CreateAsync(transport, new McpClientOptions { ProtocolVersion = McpRequestInspectorVersion },
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private const string McpRequestInspectorVersion = Gateway.API.Features.McpProxy.McpRequestInspector.SupportedProtocolVersion;

    private async Task<HttpResponseMessage> PostAsync(string? agentId, string method, string? bodyName = null, string? headerName = null,
        string serverId = "crm")
    {
        var http = _gateway.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/mcp/{serverId}")
        {
            Content = new StringContent(McpRequestInspectorTests.Body(method, bodyName), Encoding.UTF8, "application/json")
        };
        foreach (var header in McpRequestInspectorTests.Headers(method, headerName ?? bodyName))
            request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToString());
        if (agentId is not null)
            request.Headers.Add(TestAgentAuthentication.AgentHeader, agentId);

        return await http.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<int> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return json.RootElement.GetProperty("error").GetProperty("code").GetInt32();
    }

    public async ValueTask DisposeAsync()
    {
        await _gateway.DisposeAsync();
        await _mcpServer.DisposeAsync();
    }
}

/// <summary>Stands in for Keycloak: the agent id ('azp') comes from a test header.</summary>
internal sealed class TestAgentAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestAgent";
    public const string AgentHeader = "X-Test-Agent";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AgentHeader, out var agentId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity([new Claim("azp", agentId.ToString()), new Claim("sub", agentId.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
