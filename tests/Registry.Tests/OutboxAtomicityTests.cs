using Blocks.EntityFrameworkCore;
using DotNet.Testcontainers.Builders;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Registry.Application.Features.McpServers.ApplyReviewDecision;
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
/// The dual-write fix end to end on real Postgres: the ServerApproved integration event is a bus-outbox row
/// written by a domain-event handler during SaveChanges, so it commits or rolls back with the status change.
/// </summary>
public sealed class OutboxAtomicityTests : IAsyncLifetime
{
    private static readonly List<ServerStatusTransition> _transitions = ServerStatusTransitionConfiguration.LoadSeed();

    private PostgreSqlContainer? _postgres;
    private ServiceProvider? _services;

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
            .AddMassTransitTestHarness(bus => bus.AddEntityFrameworkOutbox<RegistryDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox(busOutbox => busOutbox.DisableDeliveryService()); // rows stay put for the assertions
            }))
            .BuildServiceProvider(validateScopes: true);

        await InScope(db => db.Database.MigrateAsync(TestContext.Current.CancellationToken));
        await _services.GetRequiredService<ITestHarness>().Start();
    }

    [Fact]
    public async Task Approval_and_its_integration_event_commit_together()
    {
        var serverId = await RegisterAsync("crm");

        await InScope(async (servers, factory) =>
        {
            var server = await servers.GetByIdOrThrowAsync(serverId, TestContext.Current.CancellationToken);
            server.Approve(server.PendingManifest!.Hash, factory, new ApproveServerCommand { ServerId = serverId, ActorId = "bob" });
            await servers.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await InScope(async db =>
        {
            Assert.Equal(ServerStatus.Approved, (await db.McpServers.SingleAsync(TestContext.Current.CancellationToken)).Status);
            Assert.Equal(1, await CountOutboxAsync<Contracts.ServerApproved>(db));
        });
    }

    [Fact]
    public async Task Approval_and_its_integration_event_roll_back_together()
    {
        var serverId = await RegisterAsync("crm");

        await InScope(async (servers, factory) =>
        {
            var server = await servers.GetByIdOrThrowAsync(serverId, TestContext.Current.CancellationToken);
            server.Approve(server.PendingManifest!.Hash, factory, new ApproveServerCommand { ServerId = serverId, ActorId = "bob" });
            await servers.AddAsync(NewServer("crm"), TestContext.Current.CancellationToken); // violates the unique index on Name
            await Assert.ThrowsAsync<DbUpdateException>(() => servers.SaveChangesAsync(TestContext.Current.CancellationToken));
        });

        await InScope(async db =>
        {
            var server = await db.McpServers.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(ServerStatus.UnderReview, server.Status);
            Assert.Equal(0, await CountOutboxAsync<Contracts.ServerApproved>(db));
        });
    }

    [Fact]
    public async Task Registration_and_its_ManifestSubmitted_event_commit_together()
    {
        await RegisterAsync("crm");

        await InScope(async db => Assert.Equal(1, await CountOutboxAsync<Contracts.ManifestSubmitted>(db)));
    }

    // MessageType is a list of message URNs; the contract's name identifies it
    private static Task<int> CountOutboxAsync<TMessage>(RegistryDbContext db) =>
        db.Set<OutboxMessage>().CountAsync(message => message.MessageType.Contains(":" + typeof(TMessage).Name),
            TestContext.Current.CancellationToken);

    private async Task<Guid> RegisterAsync(string name)
    {
        var server = NewServer(name);
        await InScope(async (servers, _) =>
        {
            await servers.AddAsync(server, TestContext.Current.CancellationToken);
            await servers.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return server.Id;
    }

    private static McpServer NewServer(string name) =>
        McpServer.Register(name, ServerUrl.Create("http://crm.internal/mcp"), ToolManifest.Create(ToolManifestTests.Tools),
            new RegisterServerCommand { Name = name, Url = "http://crm.internal/mcp", ActorId = "alice" });

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
