using AgentGate.Grpc.Contracts.Registry;
using AgentGate.IntegrationEvents.Registry;
using Blocks.Domain;
using Blocks.EntityFrameworkCore;
using Blocks.Exceptions;
using DotNet.Testcontainers.Builders;
using Grpc.Core;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc;
using Review.Application.Features.Grants.ApproveGrant;
using Review.Application.Features.Grants.GetGatewayGrants;
using Review.Application.Features.Grants.RequestGrant;
using Review.Application.Features.LocalCopies;
using Review.Application.Features.ManifestReviews.ApproveReview;
using Review.Application.Features.ManifestReviews.OpenReview;
using Review.Application.Features.ManifestReviews.RiskScanning;
using Review.Domain.Grants;
using Review.Domain.LocalCopies;
using Review.Domain.ManifestReviews;
using Review.Persistence;
using Review.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Contracts = AgentGate.IntegrationEvents.Review;

namespace Review.Tests;

/// <summary>One Postgres container for the class; each test migrates its own database.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer? Container { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            Container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await Container.StartAsync();
        }
        catch (DockerUnavailableException) when (Environment.GetEnvironmentVariable("CI") is null)
        {
            Container = null; // each test skips
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Container is not null)
            await Container.DisposeAsync();
    }
}

/// <summary>
/// Review on real Postgres with MassTransit's EF inbox and outbox: Registry's submissions open reviews exactly once,
/// and decisions that must be authoritative go through the (faked) Registry snapshot before anything is committed.
/// </summary>
public sealed class ReviewIntegrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string CrmHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string DriftedHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private readonly FakeRegistry _registry = new();
    private readonly Guid _serverId = Guid.CreateVersion7();
    private ServiceProvider? _services;

    private ITestHarness Harness => _services!.GetRequiredService<ITestHarness>();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        if (postgres.Container is null)
            Assert.Skip("Docker is not available; this test runs in CI.");

        var connectionString = $"{postgres.Container.GetConnectionString()};Database=review_{Guid.NewGuid():N}";
        _services = new ServiceCollection()
            .AddLogging()
            .AddDomainEventDispatch(typeof(ManifestSubmittedConsumer).Assembly)
            .AddDbContext<ReviewDbContext>((serviceProvider, options) => options
                .UseNpgsql(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<DispatchDomainEventsInterceptor>()))
            .AddScoped<ManifestReviewRepository>()
            .AddScoped<GrantRepository>()
            .AddSingleton(TestData.ReviewMachines)
            .AddSingleton(TestData.GrantMachines)
            .AddSingleton<IRiskScanner, HeuristicRiskScanner>()
            .AddSingleton<IRegistryService>(_registry)
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddConsumer<ManifestSubmittedConsumer>();
                bus.AddConsumer<AgentRegisteredConsumer>();
                bus.AddEntityFrameworkOutbox<ReviewDbContext>(outbox =>
                {
                    outbox.UsePostgres();
                    outbox.UseBusOutbox(busOutbox => busOutbox.DisableDeliveryService()); // handler rows stay put for the assertions
                });
                bus.AddConfigureEndpointsCallback((context, _, endpoint) => endpoint.UseEntityFrameworkOutbox<ReviewDbContext>(context));
            })
            .BuildServiceProvider(validateScopes: true);

        await InScope<ReviewDbContext>(db => db.Database.MigrateAsync(Ct));
        await Harness.Start();
    }

    [Fact]
    public async Task A_submitted_manifest_is_scanned_into_one_open_review_even_when_delivered_twice()
    {
        var submission = Submission(CrmHash, TestData.CrmTools, previous: null);
        var messageId = NewId.NextGuid();

        await DeliverAsync(submission, messageId);
        await RedeliverAsync(submission, messageId);

        var review = Assert.Single(await ReviewsAsync());
        Assert.Equal((ReviewStatus.Open, RiskLevel.Medium, "crm"), (review.Status, review.RiskLevel, review.ServerName));
        Assert.Equal("system:review-intake", review.CreatedBy);
        // the consumer outbox delivers after commit as a serialized send to the event's publish address
        Assert.True(await Harness.Sent.Any(message =>
            message.Context.DestinationAddress?.AbsolutePath.EndsWith(":" + nameof(Contracts.ManifestReviewOpened)) == true, Ct));
    }

    [Fact]
    public async Task Drift_supersedes_the_undecided_review_and_opens_a_high_risk_re_review()
    {
        await DeliverAsync(Submission(CrmHash, TestData.CrmTools, previous: null));

        await DeliverAsync(Submission(DriftedHash, TestData.PoisonedCrmTools, previous: TestData.CrmTools));

        var reviews = await ReviewsAsync();
        Assert.Equal(ReviewStatus.Superseded, reviews.Single(review => review.ManifestHash == CrmHash).Status);
        var reReview = reviews.Single(review => review.ManifestHash == DriftedHash);
        Assert.Equal((ReviewStatus.Open, RiskLevel.High, CrmHash), (reReview.Status, reReview.RiskLevel, reReview.PreviousHash));
        Assert.Contains(reReview.Findings, finding => finding.Rule == "hidden-instructions");
    }

    [Fact]
    public async Task An_out_of_order_older_submission_does_not_replace_the_newer_review()
    {
        var older = Submission(CrmHash, TestData.CrmTools, previous: null);
        await DeliverAsync(Submission(DriftedHash, TestData.PoisonedCrmTools, previous: TestData.CrmTools));

        await DeliverAsync(older with { OccurredOn = DateTime.UtcNow.AddMinutes(-5) });

        Assert.Equal(DriftedHash, Assert.Single(await ReviewsAsync()).ManifestHash);
    }

    [Fact]
    public async Task Final_approval_against_an_outdated_hash_is_rejected_and_nothing_is_committed()
    {
        var reviewId = await OpenReviewAsync();
        await ApproveReviewAsync(reviewId, "alice");
        _registry.Respond = _ => Snapshot("Quarantined", pendingHash: DriftedHash); // Registry has moved on

        await Assert.ThrowsAsync<DomainException>(() => ApproveReviewAsync(reviewId, "bob"));

        var review = Assert.Single(await ReviewsAsync());
        Assert.Equal(ReviewStatus.PartiallyApproved, review.Status);
        Assert.Equal(0, await CountOutboxAsync<Contracts.ManifestReviewCompleted>());
    }

    [Fact]
    public async Task Final_approval_against_the_current_hash_commits_the_decision_with_its_event()
    {
        var reviewId = await OpenReviewAsync();
        await ApproveReviewAsync(reviewId, "alice");
        Assert.Empty(_registry.Requests); // the first approval changes nothing agents see: no Registry call
        _registry.Respond = _ => Snapshot("UnderReview", pendingHash: CrmHash);

        await ApproveReviewAsync(reviewId, "bob");

        Assert.Equal(ReviewStatus.Approved, Assert.Single(await ReviewsAsync()).Status);
        Assert.Equal(_serverId, Assert.Single(_registry.Requests).ServerId);
        Assert.Equal(1, await CountOutboxAsync<Contracts.ManifestReviewCompleted>());
    }

    [Fact]
    public async Task An_unreachable_Registry_fails_closed_with_503()
    {
        var reviewId = await OpenReviewAsync();
        await ApproveReviewAsync(reviewId, "alice");
        _registry.Respond = _ => throw new RpcException(new Status(StatusCode.Unavailable, "registry is down"));

        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(() => ApproveReviewAsync(reviewId, "bob"));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal(ReviewStatus.PartiallyApproved, Assert.Single(await ReviewsAsync()).Status);
    }

    [Fact]
    public async Task A_grant_activates_after_two_approvals_and_a_live_tool_check()
    {
        var agentId = Guid.CreateVersion7();
        await DeliverAsync(new AgentRegistered(agentId, "Support bot", "demo-agent", "support-team", "Answers tickets", DateTime.UtcNow));
        await DeliverAsync(Submission(CrmHash, TestData.CrmTools, previous: null)); // makes the server known
        var grantId = await SendAsync<RequestGrantHandler, RequestGrantCommand, RequestGrantResponse>(new RequestGrantCommand
        {
            AgentId = agentId,
            ServerId = _serverId,
            Tools = ["lookup_customer"],
            Justification = "Answers questions.",
            ActorId = "admin"
        });

        await SendAsync<ApproveGrantHandler, ApproveGrantCommand>(new ApproveGrantCommand { GrantId = grantId.Id, ActorId = "alice" });
        _registry.Respond = _ => Snapshot("Approved", pendingHash: "", tools: ["export_all_customers", "lookup_customer"]);
        await SendAsync<ApproveGrantHandler, ApproveGrantCommand>(new ApproveGrantCommand { GrantId = grantId.Id, ActorId = "bob" });

        await InScope<ReviewDbContext>(async db =>
        {
            var grant = await db.Grants.SingleAsync(Ct);
            Assert.Equal((GrantStatus.Active, "demo-agent", "crm"), (grant.Status, grant.AgentClientId, grant.ServerName));
        });
        Assert.Equal(1, await CountOutboxAsync<Contracts.GrantActivated>());

        // what a restarted gateway reads back
        IReadOnlyList<GatewayGrantDto> rows = [];
        await InScope<ReviewDbContext>(async db => rows = await new GetGatewayGrantsHandler(db).Handle(new GetGatewayGrantsQuery(), Ct));
        var row = Assert.Single(rows);
        Assert.Equal(("demo-agent", "crm", "Active"), (row.AgentClientId, row.ServerName, row.Status));
        Assert.Equal(["lookup_customer"], row.Tools);
    }

    private ManifestSubmitted Submission(string hash, string toolsJson, string? previous) =>
        new(_serverId, "crm", hash, toolsJson, previous is null ? null : CrmHash, previous, "test submission", DateTime.UtcNow);

    private static ServerSnapshot Snapshot(string status, string pendingHash, List<string>? tools = null) =>
        new() { Status = status, PendingManifestHash = pendingHash, PinnedToolNames = tools ?? [] };

    private async Task<Guid> OpenReviewAsync()
    {
        await DeliverAsync(Submission(CrmHash, TestData.CrmTools, previous: null));
        return Assert.Single(await ReviewsAsync()).Id;
    }

    private Task ApproveReviewAsync(Guid reviewId, string actorId) =>
        SendAsync<ApproveReviewHandler, ApproveReviewCommand>(new ApproveReviewCommand { ReviewId = reviewId, ActorId = actorId });

    private async Task SendAsync<THandler, TCommand>(TCommand command)
        where THandler : Mediator.ICommandHandler<TCommand> where TCommand : Mediator.ICommand
    {
        await using var scope = _services!.CreateAsyncScope();
        await ActivatorUtilities.CreateInstance<THandler>(scope.ServiceProvider).Handle(command, Ct);
    }

    private async Task<TResponse> SendAsync<THandler, TCommand, TResponse>(TCommand command)
        where THandler : Mediator.ICommandHandler<TCommand, TResponse> where TCommand : Mediator.ICommand<TResponse>
    {
        await using var scope = _services!.CreateAsyncScope();
        return await ActivatorUtilities.CreateInstance<THandler>(scope.ServiceProvider).Handle(command, Ct);
    }

    /// <summary>Publishes and waits until a consumer has handled it.</summary>
    private async Task DeliverAsync<TMessage>(TMessage message, Guid? messageId = null) where TMessage : class
    {
        var id = messageId ?? NewId.NextGuid();
        await Harness.Bus.Publish(message, context => context.MessageId = id, Ct);
        var consumed = await Harness.Consumed.SelectAsync<TMessage>(received => received.Context.MessageId == id, Ct).FirstOrDefault();
        Assert.NotNull(consumed);
        Assert.Null(consumed.Exception);
    }

    /// <summary>Publishes a message the inbox has seen and waits until the inbox has received it again.</summary>
    private async Task RedeliverAsync<TMessage>(TMessage message, Guid messageId) where TMessage : class
    {
        await Harness.Bus.Publish(message, context => context.MessageId = messageId, Ct);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var receiveCount = 0;
            await InScope<ReviewDbContext>(async db => receiveCount = await db.Set<InboxState>()
                .Where(state => state.MessageId == messageId).Select(state => state.ReceiveCount).SingleOrDefaultAsync(Ct));
            if (receiveCount >= 2)
                return;
            await Task.Delay(50, Ct);
        }
        Assert.Fail("The redelivered message never reached the inbox.");
    }

    private async Task<List<ManifestReview>> ReviewsAsync()
    {
        List<ManifestReview> reviews = [];
        await InScope<ReviewDbContext>(async db => reviews = await db.ManifestReviews.AsNoTracking().ToListAsync(Ct));
        return reviews;
    }

    private async Task<int> CountOutboxAsync<TMessage>()
    {
        var count = 0;
        await InScope<ReviewDbContext>(async db => count = await db.Set<OutboxMessage>()
            .CountAsync(message => message.MessageType.Contains(":" + typeof(TMessage).Name), Ct));
        return count;
    }

    private async Task InScope<TService>(Func<TService, Task> action) where TService : notnull
    {
        await using var scope = _services!.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();
    }

    private sealed class FakeRegistry : IRegistryService
    {
        public Func<ServerSnapshotRequest, ServerSnapshot> Respond { get; set; } =
            _ => throw new InvalidOperationException("Registry was not expected to be called.");

        public List<ServerSnapshotRequest> Requests { get; } = [];

        public ValueTask<ServerSnapshot> GetServerSnapshotAsync(ServerSnapshotRequest request, CallContext context = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(Respond(request));
        }
    }
}
