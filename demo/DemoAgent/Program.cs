using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;

// A scripted "agent": authenticates as a Keycloak client, then talks MCP to the CRM server through the gateway.
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
using var host = builder.Build();
await host.StartAsync(); // starts the OpenTelemetry providers, so the run below is one exported trace

var configuration = host.Services.GetRequiredService<IConfiguration>();
var gatewayUrl = Required(configuration, "services:gateway:http:0");
var keycloakUrl = Required(configuration, "services:keycloak:http:0");
var clientId = configuration["DemoAgent:ClientId"] ?? "demo-agent";
var clientSecret = Required(configuration, "DemoAgent:ClientSecret");

var activitySource = new ActivitySource(builder.Environment.ApplicationName);
using (activitySource.StartActivity("demo-agent scenario"))
{
    using var http = new HttpClient();
    var token = await GetTokenAsync(http, keycloakUrl, clientId, clientSecret);
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var transport = new HttpClientTransport(
        new HttpClientTransportOptions { Endpoint = new Uri($"{gatewayUrl}/mcp/crm"), TransportMode = HttpTransportMode.StreamableHttp },
        http, ownsHttpClient: false);
    await using var client = await McpClient.CreateAsync(transport, new McpClientOptions { ProtocolVersion = "2026-07-28" });

    var tools = await client.ListToolsAsync();
    Console.WriteLine($"tools/list -> {string.Join(", ", tools.Select(tool => tool.Name))}");

    await CallAsync(client, "lookup_customer", new() { ["customerId"] = "C-1001" });
    await CallAsync(client, "export_all_customers", []);
}

await host.StopAsync(); // flushes telemetry

static async Task CallAsync(McpClient client, string tool, Dictionary<string, object?> arguments)
{
    try
    {
        var result = await client.CallToolAsync(tool, arguments);
        Console.WriteLine($"tools/call {tool} -> ALLOWED: {string.Join(" ", result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(block => block.Text))}");
    }
    catch (Exception exception) when (exception is HttpRequestException or ModelContextProtocol.McpException)
    {
        Console.WriteLine($"tools/call {tool} -> DENIED: {exception.Message}");
    }
}

static async Task<string> GetTokenAsync(HttpClient http, string keycloakUrl, string clientId, string clientSecret)
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

static string Required(IConfiguration configuration, string key) =>
    configuration[key] ?? throw new InvalidOperationException($"Missing configuration '{key}'.");

internal sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);
