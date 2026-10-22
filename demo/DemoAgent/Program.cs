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

// The scripted demo. As an admin: register the agent and the CRM MCP server, and request a grant.
// As two different security reviewers: approve the manifest review and the grant (four-eyes).
// As the agent: talk MCP to the server through the gateway. Then the server rug-pulls: AgentGate quarantines it
// and opens a high-risk re-review.
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
using var host = builder.Build();
await host.StartAsync(); // starts the OpenTelemetry providers, so each step below is an exported trace

var configuration = host.Services.GetRequiredService<IConfiguration>();
var gatewayUrl = Required("services:gateway:http:0");
var registryUrl = Required("services:registry:http:0");
var reviewUrl = Required("services:review:http:0");
var mcpServerUrl = Required("services:demo-mcp-server:http:0");
var keycloakUrl = Required("services:keycloak:http:0");
var scenario = configuration["DemoAgent:Scenario"] ?? "all";

var activitySource = new ActivitySource(builder.Environment.ApplicationName);
using var http = new HttpClient();

if (scenario is "all" or "setup")
    using (activitySource.StartActivity("demo: register, review and grant"))
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
    var admin = await GetTokenAsync("agentgate-admin", Required("DemoAgent:AdminClientSecret"));
    var reviewer1 = await GetTokenAsync("demo-reviewer-1", Required("DemoAgent:Reviewer1ClientSecret"));
    var reviewer2 = await GetTokenAsync("demo-reviewer-2", Required("DemoAgent:Reviewer2ClientSecret"));

    using var agent = await SendAsync(HttpMethod.Post, $"{registryUrl}/agents", admin, new
    {
        name = "Support bot",
        clientId = configuration["DemoAgent:ClientId"] ?? "demo-agent",
        owner = "support-team",
        purpose = "Answers customer questions from the CRM"
    });
    if (agent.StatusCode == HttpStatusCode.Conflict)
    {
        Console.WriteLine("registry: the demo is already set up (agent registered); skipping setup");
        return;
    }
    agent.EnsureSuccessStatusCode();
    var agentId = (await agent.Content.ReadFromJsonAsync<Created>())!.Id;
    Console.WriteLine("registry: registered agent 'demo-agent' (owner support-team)");

    using var registered = await SendAsync(HttpMethod.Post, $"{registryUrl}/servers", admin, new { name = "crm", url = $"{mcpServerUrl}/mcp" });
    registered.EnsureSuccessStatusCode();
    var server = (await registered.Content.ReadFromJsonAsync<RegisteredServer>())!;
    Console.WriteLine($"registry: registered 'crm', manifest {server.ManifestHash[..12]}, status UnderReview -> ManifestSubmitted");

    // Review opens the review asynchronously, from Registry's ManifestSubmitted event
    var review = await PollAsync(() => OpenReviewAsync(server.Id, reviewer1), "review of 'crm'");
    PrintReview(review);

    await DecideAsync($"{reviewUrl}/reviews/{review.Id}:approve", reviewer1, "demo-reviewer-1 approves the review",
        "Bulk export is expected for this CRM; access is limited by grants.");
    await DecideAsync($"{reviewUrl}/reviews/{review.Id}:approve", reviewer1, "demo-reviewer-1 tries to give the second approval too",
        "Approving again.");
    await DecideAsync($"{reviewUrl}/reviews/{review.Id}:approve", reviewer2, "demo-reviewer-2 approves the review",
        "Agreed.");
    Console.WriteLine("review: approved by two reviewers -> ManifestReviewCompleted -> Registry pins the manifest -> ServerApproved");

    var grant = await PollAsync(async () =>
    {
        // Review knows agents and servers from Registry's events; until they arrive the request is a 404
        using var requested = await SendAsync(HttpMethod.Post, $"{reviewUrl}/grants", admin, new
        {
            agentId,
            serverId = server.Id,
            tools = new[] { "lookup_customer" },
            expiresAt = DateTime.UtcNow.AddDays(30),
            justification = "Looks up the customer a ticket is about."
        });
        return requested.IsSuccessStatusCode ? await requested.Content.ReadFromJsonAsync<Created>() : null;
    }, "grant request");
    Console.WriteLine("review: grant requested: demo-agent -> crm/lookup_customer for 30 days");

    await DecideAsync($"{reviewUrl}/grants/{grant.Id}:approve", reviewer1, "demo-reviewer-1 approves the grant", null);
    // the activating approval checks Registry over gRPC: it is refused until Registry has pinned the manifest
    await PollAsync(async () =>
    {
        using var approved = await SendAsync(HttpMethod.Post, $"{reviewUrl}/grants/{grant.Id}:approve", reviewer2, new { comment = "Agreed." });
        return approved.IsSuccessStatusCode ? approved.StatusCode.ToString() : null;
    }, "grant activation");
    Console.WriteLine("review: grant activated after a live GetServerSnapshot check -> GrantActivated -> gateway");
}

async Task<ReviewSummary?> OpenReviewAsync(Guid serverId, string token)
{
    using var response = await SendAsync(HttpMethod.Get, $"{reviewUrl}/reviews?serverId={serverId}&status=Open", token);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<List<ReviewSummary>>())!.FirstOrDefault();
}

void PrintReview(ReviewSummary review)
{
    Console.WriteLine($"review: opened for manifest {review.ManifestHash[..12]}, risk {review.RiskLevel}, {review.FindingCount} finding(s)");
}

async Task DecideAsync(string url, string token, string description, string? comment)
{
    using var response = await SendAsync(HttpMethod.Post, url, token, new { comment });
    var outcome = response.IsSuccessStatusCode
        ? "OK"
        : $"REFUSED ({(int)response.StatusCode}): {(await response.Content.ReadFromJsonAsync<Problem>())?.Title}";
    Console.WriteLine($"review: {description} -> {outcome}");
}

async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
{
    using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await http.SendAsync(request);
}

// the services learn about each other through RabbitMQ: a step that depends on an earlier one may need a moment
async Task<T> PollAsync<T>(Func<Task<T?>> attempt, string what) where T : class
{
    for (var tries = 1; ; tries++)
    {
        if (await attempt() is { } value)
            return value;
        if (tries == 30)
            throw new TimeoutException($"Timed out waiting for {what}.");
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
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
            await ShowReReviewAsync();
            return;
        }
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
    Console.WriteLine("drift was not detected within 60 s; check the registry logs");
}

async Task ShowReReviewAsync()
{
    var reviewer = await GetTokenAsync("demo-reviewer-1", Required("DemoAgent:Reviewer1ClientSecret"));
    using var response = await SendAsync(HttpMethod.Get, $"{reviewUrl}/reviews?status=Open", reviewer);
    response.EnsureSuccessStatusCode();
    if ((await response.Content.ReadFromJsonAsync<List<ReviewSummary>>())!.FirstOrDefault(review => review.PreviousHash is not null) is not { } reReview)
        return;

    PrintReview(reReview);
    using var detail = await SendAsync(HttpMethod.Get, $"{reviewUrl}/reviews/{reReview.Id}", reviewer);
    foreach (var finding in (await detail.Content.ReadFromJsonAsync<ReviewDetail>())!.Findings.Where(finding => finding.Severity == "High"))
        Console.WriteLine($"review:   [{finding.Severity}] {finding.ToolName}: {finding.Rule}: {finding.Evidence}");
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

internal sealed record Created(Guid Id);

internal sealed record Problem(string? Title);

internal sealed record ReviewSummary(Guid Id, string ManifestHash, string? PreviousHash, string RiskLevel, int FindingCount);

internal sealed record ReviewDetail(IReadOnlyList<Finding> Findings);

internal sealed record Finding(string ToolName, string Rule, string Severity, string Evidence);
