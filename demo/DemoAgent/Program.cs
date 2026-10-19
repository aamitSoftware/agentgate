using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// The scripted demo. As an admin: register + approve the CRM MCP server in Registry.
// As the agent: talk MCP to it through the gateway. Then the server rug-pulls and AgentGate quarantines it.
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
using var host = builder.Build();
await host.StartAsync(); // starts the OpenTelemetry providers, so each step below is an exported trace

var configuration = host.Services.GetRequiredService<IConfiguration>();
var gatewayUrl = Required("services:gateway:http:0");
var registryUrl = Required("services:registry:http:0");
var mcpServerUrl = Required("services:demo-mcp-server:http:0");
var keycloakUrl = Required("services:keycloak:http:0");
var scenario = configuration["DemoAgent:Scenario"] ?? "all";

var activitySource = new ActivitySource(builder.Environment.ApplicationName);
using var http = new HttpClient();

if (scenario is "all" or "setup")
    using (activitySource.StartActivity("demo: register and approve the CRM server"))
        await SetUpAsync();

await using var agent = await ConnectAgentAsync();

if (scenario is "all" or "call")
    using (activitySource.StartActivity("demo: agent calls tools"))
    {
        await ListToolsAsync();
        await CallAsync("lookup_customer", new() { ["customerId"] = "C-1001" });
        await CallAsync("export_all_customers", []);
    }

if (scenario is "all" or "rugpull")
    using (activitySource.StartActivity("demo: rug pull"))
        await RugPullAsync();

await host.StopAsync(); // flushes telemetry
return;

async Task SetUpAsync()
{
    using var request = new HttpRequestMessage(HttpMethod.Post, $"{registryUrl}/servers")
    {
        Content = JsonContent.Create(new { name = "crm", url = $"{mcpServerUrl}/mcp" })
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
        await GetTokenAsync("agentgate-admin", Required("DemoAgent:AdminClientSecret")));

    using var registered = await http.SendAsync(request);
    if (registered.StatusCode == HttpStatusCode.Conflict)
    {
        Console.WriteLine("registry: 'crm' is already registered");
        return;
    }
    registered.EnsureSuccessStatusCode();
    var server = (await registered.Content.ReadFromJsonAsync<RegisteredServer>())!;
    Console.WriteLine($"registry: registered 'crm', manifest {server.ManifestHash[..12]}, status UnderReview");

    using var approve = new HttpRequestMessage(HttpMethod.Post, $"{registryUrl}/servers/{server.Id}:approve")
    {
        Content = JsonContent.Create(new { comment = "Demo approval" })
    };
    approve.Headers.Authorization = request.Headers.Authorization;
    (await http.SendAsync(approve)).EnsureSuccessStatusCode();
    Console.WriteLine("registry: approved 'crm' -> ServerApproved goes out through the outbox");
}

async Task<McpClient> ConnectAgentAsync()
{
    var agentHttp = new HttpClient();
    agentHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
        await GetTokenAsync(configuration["DemoAgent:ClientId"] ?? "demo-agent", Required("DemoAgent:ClientSecret")));
    var transport = new HttpClientTransport(
        new HttpClientTransportOptions { Endpoint = new Uri($"{gatewayUrl}/mcp/crm"), TransportMode = HttpTransportMode.StreamableHttp },
        agentHttp, ownsHttpClient: true);

    // the gateway learns about the approval asynchronously (RabbitMQ): until then 'crm' does not exist for agents
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            return await McpClient.CreateAsync(transport, new McpClientOptions { ProtocolVersion = "2026-07-28" });
        }
        catch (Exception exception) when (attempt < 15 && exception is HttpRequestException or McpException)
        {
            Console.WriteLine("gateway: 'crm' not pinned yet, retrying...");
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

async Task RugPullAsync()
{
    (await http.PostAsync($"{mcpServerUrl}/admin/rug-pull", null)).EnsureSuccessStatusCode();
    Console.WriteLine("mcp server: lookup_customer's description now carries hidden instructions");

    for (var attempt = 0; attempt < 30; attempt++)
    {
        await ListToolsAsync(); // the agent keeps seeing the pinned, approved description
        if (!await CallAsync("lookup_customer", new() { ["customerId"] = "C-1001" }))
        {
            Console.WriteLine("gateway: server quarantined; the poisoned description never reached the agent");
            return;
        }
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
    Console.WriteLine("drift was not detected within 60 s; check the registry logs");
}

async Task ListToolsAsync()
{
    try
    {
        var tools = await agent.ListToolsAsync();
        foreach (var tool in tools)
            Console.WriteLine($"tools/list -> {tool.Name}: {tool.Description}");
    }
    catch (Exception exception) when (exception is HttpRequestException or McpException)
    {
        Console.WriteLine($"tools/list -> DENIED: {exception.Message}");
    }
}

async Task<bool> CallAsync(string tool, Dictionary<string, object?> arguments)
{
    try
    {
        var result = await agent.CallToolAsync(tool, arguments);
        Console.WriteLine($"tools/call {tool} -> ALLOWED: {string.Join(" ", result.Content.OfType<TextContentBlock>().Select(block => block.Text))}");
        return true;
    }
    catch (Exception exception) when (exception is HttpRequestException or McpException)
    {
        Console.WriteLine($"tools/call {tool} -> DENIED: {exception.Message}");
        return false;
    }
}

async Task<string> GetTokenAsync(string clientId, string clientSecret)
{
    using var response = await http.PostAsync($"{keycloakUrl}/realms/agentgate/protocol/openid-connect/token",
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        }));
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
}

string Required(string key) =>
    configuration[key] ?? throw new InvalidOperationException($"Missing configuration '{key}'.");

internal sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

internal sealed record RegisteredServer(Guid Id, string ManifestHash);
