using AgentGate.Grpc.Contracts.Registry;
using Blocks.EntityFrameworkCore;
using DotNet.Testcontainers.Builders;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Server;
using Registry.API.Grpc;
using Registry.Application.Features.McpServers.ApplyReviewDecision;
using Registry.Application.Features.McpServers.RegisterServer;
using Registry.Application.StateMachines;
using Registry.Domain.McpServers;
using Registry.Persistence;
using Registry.Persistence.EntityConfigurations;
using Registry.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Registry.Tests;

/// <summary>The one gRPC contract over a real HTTP/2 channel (in memory), code-first on both ends, against Postgres.</summary>
public sealed class RegistryGrpcServiceTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private WebApplication? _app;
    private GrpcChannel? _channel;

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

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddDomainEventDispatch(typeof(ManifestReviewCompletedConsumer).Assembly)
            .AddDbContext<RegistryDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()))
            .AddScoped<McpServerRepository>()
            .AddCodeFirstGrpc();
        _app = builder.Build();
        _app.MapGrpcService<RegistryGrpcService>();
        await _app.StartAsync(TestContext.Current.CancellationToken);

        await using (var scope = _app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RegistryDbContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);

        _channel = GrpcChannel.ForAddress("http://registry", new GrpcChannelOptions { HttpHandler = _app.GetTestServer().CreateHandler() });
    }

    [Fact]
    public async Task The_snapshot_reports_status_both_hashes_and_the_pinned_tool_names()
    {
        var manifest = ToolManifest.Create(ToolManifestTests.Tools);
        var server = McpServer.Register("crm", ServerUrl.Create("http://crm.internal/mcp"), manifest,
            new RegisterServerCommand { Name = "crm", Url = "http://crm.internal/mcp", ActorId = "alice" });
        var transitions = ServerStatusTransitionConfiguration.LoadSeed();
        server.Approve(manifest.Hash, status => new ServerStateMachine(status, transitions), new ApproveServerCommand { ActorId = "bob" });
        await using (var scope = _app!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
            server.ClearDomainEvents(); // no bus in this test
            db.McpServers.Add(server);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var snapshot = await _channel!.CreateGrpcService<IRegistryService>()
            .GetServerSnapshotAsync(new ServerSnapshotRequest { ServerId = server.Id });

        Assert.Equal("Approved", snapshot.Status);
        Assert.Equal(manifest.Hash, snapshot.PinnedManifestHash);
        Assert.Equal("", snapshot.PendingManifestHash);
        Assert.Equal(manifest.ToolNames, snapshot.PinnedToolNames);
    }

    [Fact]
    public async Task An_unknown_server_is_an_RpcException_the_caller_treats_as_cannot_verify()
    {
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await _channel!.CreateGrpcService<IRegistryService>()
            .GetServerSnapshotAsync(new ServerSnapshotRequest { ServerId = Guid.CreateVersion7() }));

        Assert.NotEqual(StatusCode.OK, exception.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _channel?.Dispose();
        if (_app is not null)
            await _app.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }
}
