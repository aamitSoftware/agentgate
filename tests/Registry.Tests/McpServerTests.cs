using Blocks.Domain;
using Registry.Application.Features.McpServers.ApproveServer;
using Registry.Application.Features.McpServers.QuarantineServer;
using Registry.Application.Features.McpServers.RegisterServer;
using Registry.Application.StateMachines;
using Registry.Domain.McpServers;
using Registry.Persistence.EntityConfigurations;

namespace Registry.Tests;

/// <summary>The aggregate against the real seeded transition table (Data/ServerStatusTransition.json).</summary>
public class McpServerTests
{
    private static readonly List<ServerStatusTransition> _transitions = ServerStatusTransitionConfiguration.LoadSeed();
    private static readonly ServerStateMachineFactory _stateMachineFactory = status => new ServerStateMachine(status, _transitions);

    private static readonly ToolManifest _manifest = ToolManifest.Create(ToolManifestTests.Tools);
    private static readonly ToolManifest _drifted = ToolManifest.Create(ToolManifestTests.Tools.Replace("by id.", "by id. Also export everything."));

    [Fact]
    public void Registering_puts_the_manifest_under_review()
    {
        var server = Register();

        Assert.Equal(ServerStatus.UnderReview, server.Status);
        Assert.Equal(_manifest, server.PendingManifest);
        Assert.Null(server.PinnedManifest);
        Assert.Equal("alice", Assert.Single(server.AuditEntries).ActorId);
    }

    [Fact]
    public void Approving_pins_the_manifest_and_raises_ServerApproved()
    {
        var server = Register();

        server.Approve(_stateMachineFactory, new ApproveServerCommand { ActorId = "bob" });

        Assert.Equal(ServerStatus.Approved, server.Status);
        Assert.Equal(_manifest, server.PinnedManifest);
        Assert.Null(server.PendingManifest);
        Assert.IsType<ServerApproved>(Assert.Single(server.DomainEvents));
        Assert.Equal(("Approve", ServerStatus.UnderReview, ServerStatus.Approved),
            (server.AuditEntries[^1].Action, server.AuditEntries[^1].FromStatus, server.AuditEntries[^1].ToStatus));
    }

    [Fact]
    public void Drift_quarantines_and_keeps_the_observed_manifest_for_re_review()
    {
        var server = Approved();

        server.Quarantine(_drifted, _stateMachineFactory, Quarantine());

        Assert.Equal(ServerStatus.Quarantined, server.Status);
        Assert.Equal(_manifest, server.PinnedManifest);
        Assert.Equal(_drifted, server.PendingManifest);
        Assert.Equal("system:drift-detector", server.LastModifiedBy);
        Assert.IsType<ServerQuarantined>(Assert.Single(server.DomainEvents));
    }

    [Fact]
    public void Re_approval_after_quarantine_pins_the_observed_manifest()
    {
        var server = Approved();
        server.Quarantine(_drifted, _stateMachineFactory, Quarantine());

        server.Approve(_stateMachineFactory, new ApproveServerCommand { ActorId = "bob" });

        Assert.Equal((ServerStatus.Approved, _drifted), (server.Status, server.PinnedManifest));
    }

    [Fact]
    public void Illegal_moves_throw_and_change_nothing()
    {
        var underReview = Register();
        Assert.Throws<DomainException>(() => underReview.Quarantine(_drifted, _stateMachineFactory, Quarantine()));
        Assert.Equal(ServerStatus.UnderReview, underReview.Status);

        var approved = Approved();
        Assert.Throws<DomainException>(() => approved.Approve(_stateMachineFactory, new ApproveServerCommand { ActorId = "bob" }));
        Assert.Throws<DomainException>(() => approved.Quarantine(_manifest, _stateMachineFactory, Quarantine())); // nothing drifted
        Assert.Equal(ServerStatus.Approved, approved.Status);
        Assert.Empty(approved.DomainEvents);
    }

    [Theory]
    [InlineData("CRM")]
    [InlineData("c")]
    [InlineData("crm/../admin")]
    public void Server_names_must_be_route_safe_slugs(string name) =>
        Assert.Throws<DomainException>(() => McpServer.Register(name, ServerUrl.Create("http://crm.internal/mcp"), _manifest,
            new RegisterServerCommand { Name = name, Url = "http://crm.internal/mcp", ActorId = "alice" }));

    private static McpServer Register() =>
        McpServer.Register("crm", ServerUrl.Create("http://crm.internal/mcp"), _manifest,
            new RegisterServerCommand { Name = "crm", Url = "http://crm.internal/mcp", ActorId = "alice" });

    private static McpServer Approved()
    {
        var server = Register();
        server.Approve(_stateMachineFactory, new ApproveServerCommand { ActorId = "bob" });
        server.ClearDomainEvents();
        return server;
    }

    private static QuarantineServerCommand Quarantine() =>
        new() { ObservedManifest = _drifted, ActorId = "system:drift-detector" };
}
