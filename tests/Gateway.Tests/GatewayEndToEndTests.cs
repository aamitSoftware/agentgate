using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DemoMcpServer;
using Gateway.API;
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

    public ValueTask InitializeAsync()
    {
        _gateway = new WebApplicationFactory<GatewayOptions>().WithWebHostBuilder(web =>
        {
            web.UseSetting("GatewayOptions:Servers:crm:Url", "http://demo-mcp-server/mcp");
            web.ConfigureTestServices(services =>
            {
                services.AddSingleton(new HttpMessageInvoker(_mcpServer.Server.CreateHandler()));
                services.AddAuthentication(TestAgentAuthentication.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAgentAuthentication>(TestAgentAuthentication.SchemeName, null);
            });
        });
        return ValueTask.CompletedTask;
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

    private async Task<HttpResponseMessage> PostAsync(string? agentId, string method, string? bodyName = null, string? headerName = null)
    {
        var http = _gateway.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/crm")
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
