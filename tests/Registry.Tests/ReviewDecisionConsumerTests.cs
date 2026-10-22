using AgentGate.IntegrationEvents.Review;
using Blocks.EntityFrameworkCore;
using DotNet.Testcontainers.Builders;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Registry.Application.Features.McpServers.ApplyReviewDecision;
using Registry.Application.Features.McpServers.GetGatewayServers;
using Registry.Application.Features.McpServers.QuarantineServer;
using Registry.Application.Features.McpServers.RegisterServer;
using Registry.Application.StateMachines;
using Registry.Domain.McpServers;
using Registry.Persistence;
using Registry.Persistence.EntityConfigurations;
using Registry.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Contracts = AgentGate.IntegrationEvents.Registry;

namespace Registry.Tests;

/// <summary>
/// Review's decision reaches Registry as an event, on real Postgres with the EF inbox on the endpoint:
/// approval pins the manifest, a redelivered decision is a no-op, and a stale decision is refused.
/// </summary>
public sealed class ReviewDecisionConsumerTests : IAsyncLifetime
{
    private static readonly List<ServerStatusTransition> _transitions = ServerStatusTransitionConfiguration.LoadSeed();
    private static readonly ToolManifest _manifest = ToolManifest.Create(ToolManifestTests.Tools);
    private static readonly ToolManifest _drifted = ToolManifest.Create(ToolManifestTests.Tools.Replace("by id.", "by id. Also export everything."));

    private PostgreSqlContainer? _postgres;
    private ServiceProvider? _services;

    private ITestHarness Harness => _services!.GetRequiredService<ITestHarness>();

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

        _services = new ServiceCollection()
            .AddDomainEventDispatch(typeof(ManifestReviewCompletedConsumer).Assembly)
            .AddDbContext<RegistryDbContext>((serviceProvider, options) => options
                .UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(serviceProvider.GetRequiredService<DispatchDomainEventsInterceptor>()))
            .AddScoped<McpServerRepository>()
            .AddSingleton<ServerStateMachineFactory>(status => new ServerStateMachine(status, _transitions))
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddConsumer<ManifestReviewCompletedConsumer>();
                bus.AddEntityFrameworkOutbox<RegistryDbContext>(outbox => outbox.UsePostgres());
                // the same endpoint wiring as Blocks.Messaging: inbox + consumer outbox on every endpoint
                bus.AddConfigureEndpointsCallback((context, _, endpoint) => endpoint.UseEntityFrameworkOutbox<RegistryDbContext>(context));
            })
            .BuildServiceProvider(validateScopes: true);

        await InScope(db => db.Database.MigrateAsync(TestContext.Current.CancellationToken));
        await Harness.Start();
    }

    [Fact]
    public async Task An_approved_review_pins_the_manifest_and_tells_the_gateway()
    {
        var serverId = await RegisterAsync();

        await DeliverAsync(Decision(serverId, _manifest.Hash, ManifestReviewCompletedConsumer.Approved));

        var server = await LoadAsync(serverId);
        Assert.Equal((ServerStatus.Approved, _manifest), (server.Status, server.PinnedManifest));
        Assert.Equal("bob", server.AuditEntries[^1].ActorId); // the second reviewer completed the decision
        Assert.Contains("alice, bob", server.AuditEntries[^1].Comment);
        Assert.True(await DeliveredAsync<Contracts.ServerApproved>());
    }

    [Fact]
    public async Task A_redelivered_decision_changes_state_once()
    {
        var serverId = await RegisterAsync();
        var decision = Decision(serverId, _manifest.Hash, ManifestReviewCompletedConsumer.Approved);
        var messageId = NewId.NextGuid();

        await DeliverAsync(decision, messageId);
        await RedeliverAsync(decision, messageId); // without the inbox this would fault: nothing is pending any more

        var server = await LoadAsync(serverId);
        Assert.Single(server.AuditEntries, entry => entry.Action == nameof(ServerActionType.Approve));
        Assert.False(await FaultedAsync());
    }

    [Fact]
    public async Task A_decision_on_an_outdated_manifest_faults_and_leaves_the_server_quarantined()
    {
        var serverId = await RegisterAsync();
        await DeliverAsync(Decision(serverId, _manifest.Hash, ManifestReviewCompletedConsumer.Approved));
        await InScope(async (servers, factory) =>
        {
            var server = await servers.GetByIdOrThrowAsync(serverId, TestContext.Current.CancellationToken);
            server.Quarantine(_drifted, factory, new QuarantineServerCommand { ServerId = serverId, ObservedManifest = _drifted, ActorId = "system:drift-detector" });
            await servers.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        // a late approval of the original manifest must not re-open the server
        await DeliverAsync(Decision(serverId, _manifest.Hash, ManifestReviewCompletedConsumer.Approved));

        Assert.True(await FaultedAsync());
        var server = await LoadAsync(serverId);
        Assert.Equal((ServerStatus.Quarantined, _drifted), (server.Status, server.PendingManifest));
    }

    [Fact]
    public async Task The_gateway_snapshot_lists_pinned_servers_with_their_last_change()
    {
        var approvedId = await RegisterAsync();
        await DeliverAsync(Decision(approvedId, _manifest.Hash, ManifestReviewCompletedConsumer.Approved));

        IReadOnlyList<GatewayServerDto> rows = [];
        await InScope(async db => rows = await new GetGatewayServersHandler(db).Handle(new GetGatewayServersQuery(), TestContext.Current.CancellationToken));

        var row = Assert.Single(rows); // nothing else is pinned
        Assert.Equal(("crm", "Approved", _manifest.Hash, _manifest.ToolsJson), (row.Name, row.Status, row.ManifestHash, row.ToolsJson));
        Assert.True(row.AsOf > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task A_rejected_review_keeps_the_server_out_of_service()
    {
        var serverId = await RegisterAsync();

        await DeliverAsync(Decision(serverId, _manifest.Hash, ManifestReviewCompletedConsumer.Rejected));

        Assert.Equal(ServerStatus.Rejected, (await LoadAsync(serverId)).Status);
        Assert.True(await DeliveredAsync<Contracts.ServerRejected>());
    }

    private static ManifestReviewCompleted Decision(Guid serverId, string hash, string decision) =>
        new(Guid.CreateVersion7(), serverId, hash, decision, "Medium", ["alice", "bob"], null, DateTime.UtcNow);

    /// <summary>Publishes and waits until the consumer has handled it (successfully or with a fault).</summary>
    private async Task DeliverAsync(ManifestReviewCompleted decision, Guid? messageId = null)
    {
        var id = messageId ?? NewId.NextGuid();
        await Harness.Bus.Publish(decision, context => context.MessageId = id, TestContext.Current.CancellationToken);
        Assert.True(await Harness.Consumed.Any<ManifestReviewCompleted>(message => message.Context.MessageId == id,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Publishes a message the inbox has already seen and waits until the inbox has received it again.
    /// A duplicate never reaches the consumer, so there is no "consumed" signal to wait for.
    /// </summary>
    private async Task RedeliverAsync(ManifestReviewCompleted decision, Guid messageId)
    {
        await Harness.Bus.Publish(decision, context => context.MessageId = messageId, TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var receiveCount = 0;
            await InScope(async db => receiveCount = await db.Set<InboxState>().Where(state => state.MessageId == messageId)
                .Select(state => state.ReceiveCount).SingleOrDefaultAsync(TestContext.Current.CancellationToken));
            if (receiveCount >= 2)
                return;
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        Assert.Fail("The redelivered message never reached the inbox.");
    }

    // the consumer outbox delivers after commit as a serialized send to the message type's publish address
    private Task<bool> DeliveredAsync<TMessage>() where TMessage : class =>
        Harness.Sent.Any(message => message.Context.DestinationAddress?.AbsolutePath.EndsWith(":" + typeof(TMessage).Name) == true,
            TestContext.Current.CancellationToken);

    private Task<bool> FaultedAsync() =>
        Harness.Consumed.Any<ManifestReviewCompleted>(message => message.Exception is not null, TestContext.Current.CancellationToken);

    private async Task<Guid> RegisterAsync()
    {
        var server = McpServer.Register("crm", ServerUrl.Create("http://crm.internal/mcp"), _manifest,
            new RegisterServerCommand { Name = "crm", Url = "http://crm.internal/mcp", ActorId = "alice" });
        await InScope(async (servers, _) =>
        {
            await servers.AddAsync(server, TestContext.Current.CancellationToken);
            await servers.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return server.Id;
    }

    private async Task<McpServer> LoadAsync(Guid serverId)
    {
        McpServer? server = null;
        await InScope(async (servers, _) => server = await servers.GetByIdOrThrowAsync(serverId, TestContext.Current.CancellationToken));
        return server!;
    }

    private Task InScope(Func<McpServerRepository, ServerStateMachineFactory, Task> action) =>
        InNewScope(scope => action(scope.GetRequiredService<McpServerRepository>(), scope.GetRequiredService<ServerStateMachineFactory>()));

    private Task InScope(Func<RegistryDbContext, Task> action) =>
        InNewScope(scope => action(scope.GetRequiredService<RegistryDbContext>()));

    private async Task InNewScope(Func<IServiceProvider, Task> action)
    {
        await using var scope = _services!.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }
}
