using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using AgentGate.IntegrationEvents.Gateway;
using AgentGate.IntegrationEvents.Registry;
using AgentGate.IntegrationEvents.Review;
using DotNet.Testcontainers.Builders;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Hub.Tests;

/// <summary>
/// The real Hub API in memory on real Postgres, fed through an in-memory bus: the rug-pull story as events,
/// read back as the console reads it. Only Keycloak is replaced, by a header-driven test scheme.
/// </summary>
public sealed class HubApiTests : IAsyncLifetime
{
    private const string PinnedHash = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string DriftedHash = "2222222222222222222222222222222222222222222222222222222222222222";
    private const string ToolsJson = """[{"name":"export_all_customers"},{"name":"lookup_customer"}]""";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly Guid _serverId = Guid.CreateVersion7();
    private readonly Guid _agentId = Guid.CreateVersion7();
    private readonly Guid _grantId = Guid.CreateVersion7();
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _hub;

    private ITestHarness Harness => _hub!.Services.GetTestHarness();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _postgres.StartAsync();
        }
        catch (DockerUnavailableException) when (Environment.GetEnvironmentVariable("CI") is null)
        {
            Assert.Skip("Docker is not available; this test runs in CI.");
        }

        _hub = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseSetting("ConnectionStrings:hubdb", _postgres.GetConnectionString());
            web.UseSetting("ConnectionStrings:rabbitmq", "amqp://localhost"); // never dialled: the harness swaps in an in-memory bus
            web.ConfigureTestServices(services =>
            {
                services.AddMassTransitTestHarness(bus => bus.AddConsumers(typeof(Hub.Application.DependencyInjection).Assembly));
                services.AddAuthentication(TestUserAuthentication.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestUserAuthentication>(TestUserAuthentication.SchemeName, null);
            });
        });
        _hub.CreateClient(); // starts the host: migrations run, the bus starts
        Harness.TestInactivityTimeout = TimeSpan.FromSeconds(5); // CI runners are slower than a laptop
        Harness.TestTimeout = TimeSpan.FromSeconds(60);
    }

    [Fact]
    public async Task The_rug_pull_story_reads_back_as_inventory_access_and_timeline()
    {
        await PlayRugPullAsync();

        var server = Assert.Single(await GetAsync<List<JsonElement>>("/servers"));
        Assert.Equal("Quarantined", server.GetProperty("status").GetString());
        Assert.Equal(PinnedHash, server.GetProperty("pinnedHash").GetString());
        Assert.Equal(DriftedHash, server.GetProperty("pendingHash").GetString());
        Assert.Equal("High", server.GetProperty("openReviewRisk").GetString());
        Assert.Equal(2, server.GetProperty("toolCount").GetInt32());

        var access = Assert.Single(await GetAsync<List<JsonElement>>("/access"));
        Assert.Equal(("demo-agent", "crm"), (access.GetProperty("agentClientId").GetString(), access.GetProperty("serverName").GetString()));

        var detail = await GetAsync<JsonElement>($"/servers/{_serverId}");
        var kinds = detail.GetProperty("timeline").EnumerateArray().Select(entry => entry.GetProperty("eventKind").GetString()).ToList();
        Assert.Equal(
            ["ManifestReviewOpened", "ManifestSubmitted", "ServerQuarantined", "GrantActivated", "ServerApproved", "ManifestReviewCompleted",
             "ManifestReviewOpened", "ManifestSubmitted"],
            kinds); // newest first
        var quarantine = detail.GetProperty("timeline").EnumerateArray().Single(entry => entry.GetProperty("eventKind").GetString() == "ServerQuarantined");
        Assert.Equal("critical", quarantine.GetProperty("severity").GetString());
        Assert.Equal(2, detail.GetProperty("pinnedTools").GetArrayLength());

        var agent = Assert.Single(await GetAsync<List<JsonElement>>("/agents"));
        Assert.Equal((1, 1), (agent.GetProperty("activeGrantCount").GetInt32(), agent.GetProperty("deniedLastHour").GetInt32()));
    }

    [Fact]
    public async Task An_older_status_event_does_not_overwrite_a_newer_one()
    {
        var now = DateTime.UtcNow;
        await DeliverAsync(new ServerQuarantined(_serverId, "crm", PinnedHash, DriftedHash, "drift", now));
        await DeliverAsync(new ServerApproved(_serverId, "crm", "http://crm/mcp", PinnedHash, ToolsJson, now.AddSeconds(-5))); // arrives late

        var server = Assert.Single(await GetAsync<List<JsonElement>>("/servers"));
        Assert.Equal("Quarantined", server.GetProperty("status").GetString());
        Assert.Equal("http://crm/mcp", server.GetProperty("url").GetString()); // the facts it carries still apply
    }

    [Fact]
    public async Task The_live_stream_pushes_activity_as_server_sent_events()
    {
        using var client = Client(Roles: "auditor");
        using var response = await client.GetAsync("/activity/stream", HttpCompletionOption.ResponseHeadersRead, Ct);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(Ct));

        // published without waiting on the harness: the assertion is that it arrives on the open stream
        await Harness.Bus.Publish(new ToolCallObserved(NewId.NextGuid(), "demo-agent", "crm", "tools/call", "export_all_customers", "Denied",
            "Tool 'export_all_customers' is not granted to this agent.", null, DateTime.UtcNow), Ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        // skip heartbeats (the first one opens the stream) until the activity event's data line
        string? line, eventType = null;
        while ((line = await reader.ReadLineAsync(timeout.Token)) is not null)
        {
            if (line.StartsWith("event:", StringComparison.Ordinal))
                eventType = line["event:".Length..].Trim();
            else if (line.StartsWith("data:", StringComparison.Ordinal) && eventType == "activity")
                break;
        }
        var item = JsonSerializer.Deserialize<JsonElement>(line!["data:".Length..], _json);
        Assert.Equal(("call", "Denied", "warning"), (item.GetProperty("kind").GetString(), item.GetProperty("decision").GetString(),
            item.GetProperty("severity").GetString()));
    }

    [Fact]
    public async Task An_anomaly_is_a_critical_entry_on_the_agents_timeline()
    {
        await DeliverAsync(new AgentRegistered(_agentId, "Support bot", "demo-agent", "support-team", "Answers tickets", DateTime.UtcNow.AddMinutes(-1)));

        await DeliverAsync(new AnomalyDetected(NewId.NextGuid(), "demo-agent", "DeniedCallSpike", 5, 60, ["crm/export_all_customers"], DateTime.UtcNow));

        var agent = await GetAsync<JsonElement>($"/agents/{_agentId}");
        var entry = agent.GetProperty("timeline").EnumerateArray().First();
        Assert.Equal(("AnomalyDetected", "critical"), (entry.GetProperty("eventKind").GetString(), entry.GetProperty("severity").GetString()));
        Assert.Contains("5 calls denied", entry.GetProperty("summary").GetString());
    }

    [Fact]
    public async Task Reads_need_a_governance_role()
    {
        using var anonymous = _hub!.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/servers", Ct)).StatusCode);

        using var agent = Client(Roles: "");
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/servers", Ct)).StatusCode);
    }

    private async Task PlayRugPullAsync()
    {
        var start = DateTime.UtcNow.AddMinutes(-10);
        var reviewId = Guid.CreateVersion7();
        await DeliverAsync(new AgentRegistered(_agentId, "Support bot", "demo-agent", "support-team", "Answers tickets", start));
        await DeliverAsync(new ManifestSubmitted(_serverId, "crm", PinnedHash, ToolsJson, null, null, "Register", start.AddSeconds(1)));
        await DeliverAsync(new ManifestReviewOpened(reviewId, _serverId, "crm", PinnedHash, null, "Medium", 1, start.AddSeconds(2)));
        await DeliverAsync(new ManifestReviewCompleted(reviewId, _serverId, PinnedHash, "Approved", "Medium", ["alice", "bob"], "ok", start.AddSeconds(3)));
        await DeliverAsync(new GrantActivated(_grantId, _agentId, "demo-agent", _serverId, "crm", ["lookup_customer"], null, start.AddSeconds(4)));
        await DeliverAsync(new ServerApproved(_serverId, "crm", "http://crm/mcp", PinnedHash, ToolsJson, start.AddSeconds(3.5)));
        await DeliverAsync(new ToolCallObserved(NewId.NextGuid(), "demo-agent", "crm", "tools/call", "export_all_customers", "Denied",
            "not granted", null, DateTime.UtcNow));
        await DeliverAsync(new ServerQuarantined(_serverId, "crm", PinnedHash, DriftedHash, "Manifest drifted.", start.AddSeconds(5)));
        await DeliverAsync(new ManifestSubmitted(_serverId, "crm", DriftedHash, ToolsJson, PinnedHash, ToolsJson, "drift", start.AddSeconds(5.5)));
        await DeliverAsync(new ManifestReviewOpened(Guid.CreateVersion7(), _serverId, "crm", DriftedHash, PinnedHash, "High", 4, start.AddSeconds(6)));
    }

    private async Task DeliverAsync<TMessage>(TMessage message) where TMessage : class
    {
        var id = NewId.NextGuid();
        await Harness.Bus.Publish(message, context => context.MessageId = id, Ct);
        var consumed = await Harness.Consumed.SelectAsync<TMessage>(received => received.Context.MessageId == id, Ct).FirstOrDefault();
        Assert.NotNull(consumed);
        Assert.Null(consumed.Exception);
    }

    private async Task<T> GetAsync<T>(string path)
    {
        using var client = Client(Roles: "security-reviewer");
        return (await client.GetFromJsonAsync<T>(path, _json, Ct))!;
    }

    private HttpClient Client(string Roles)
    {
        var client = _hub!.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserAuthentication.UserHeader, "alice");
        client.DefaultRequestHeaders.Add(TestUserAuthentication.RolesHeader, Roles);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }
}

/// <summary>Stands in for Keycloak: the user and roles come from test headers.</summary>
internal sealed class TestUserAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestUser";
    public const string UserHeader = "X-Test-User";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user))
            return Task.FromResult(AuthenticateResult.NoResult());

        var roles = Request.Headers[RolesHeader].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        var identity = new ClaimsIdentity([new Claim("sub", user.ToString()), .. roles.Select(role => new Claim("roles", role))],
            SchemeName, "sub", "roles");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
