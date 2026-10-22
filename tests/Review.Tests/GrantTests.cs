using Blocks.Domain;
using Review.Domain.Grants;
using Review.Domain.LocalCopies;

namespace Review.Tests;

public class GrantTests
{
    private static readonly KnownAgent _agent = KnownAgent.Create(Guid.CreateVersion7(), "Support bot", "demo-agent", "support-team", "Answers tickets");
    private static readonly KnownServer _server = KnownServer.Create(Guid.CreateVersion7(), "crm");
    private static readonly CurrentServerState _approvedCrm = new(IsApproved: true, ["export_all_customers", "lookup_customer"]);

    [Fact]
    public void Two_different_reviewers_activate_a_grant()
    {
        var grant = Requested();

        grant.Approve(null, TestData.GrantMachines, Action(GrantActionType.Approve, "alice"));
        Assert.Equal(GrantStatus.PendingApproval, grant.Status);
        grant.Approve(_approvedCrm, TestData.GrantMachines, Action(GrantActionType.Approve, "bob"));

        Assert.Equal(GrantStatus.Active, grant.Status);
        var activated = Assert.IsType<GrantActivated>(Assert.Single(grant.DomainEvents));
        Assert.Equal(("demo-agent", "crm"), (activated.Grant.AgentClientId, activated.Grant.ServerName));
    }

    [Fact]
    public void One_person_cannot_give_both_approvals()
    {
        var grant = Requested();
        grant.Approve(null, TestData.GrantMachines, Action(GrantActionType.Approve, "alice"));

        Assert.Throws<DomainException>(() => grant.Approve(_approvedCrm, TestData.GrantMachines, Action(GrantActionType.Approve, "alice")));
        Assert.Equal(GrantStatus.PendingApproval, grant.Status);
    }

    [Fact]
    public void Activation_needs_the_server_approved_right_now()
    {
        var grant = PendingApproval();

        Assert.Throws<DomainException>(() => grant.Approve(new(IsApproved: false, ["lookup_customer"]), TestData.GrantMachines,
            Action(GrantActionType.Approve, "bob")));
        Assert.Throws<DomainException>(() => grant.Approve(null, TestData.GrantMachines, Action(GrantActionType.Approve, "bob")));
        Assert.Equal(GrantStatus.PendingApproval, grant.Status);
    }

    [Fact]
    public void Activation_needs_every_granted_tool_in_the_approved_manifest()
    {
        var grant = PendingApproval(tools: ["lookup_customer", "delete_customer"]);

        var exception = Assert.Throws<DomainException>(() => grant.Approve(_approvedCrm, TestData.GrantMachines, Action(GrantActionType.Approve, "bob")));
        Assert.Contains("delete_customer", exception.Message);
    }

    [Fact]
    public void Suspend_resume_and_revoke_follow_the_lifecycle()
    {
        var grant = Active();

        grant.Suspend(TestData.GrantMachines, Action(GrantActionType.Suspend, "system:anomaly-detector", "Denied-call spike."));
        Assert.Equal(GrantStatus.Suspended, grant.Status);
        grant.Resume(TestData.GrantMachines, Action(GrantActionType.Resume, "alice"));
        Assert.Equal(GrantStatus.Active, grant.Status);
        grant.Revoke(TestData.GrantMachines, Action(GrantActionType.Revoke, "alice", "Project ended."));

        Assert.Equal(GrantStatus.Revoked, grant.Status);
        Assert.Collection(grant.DomainEvents,
            domainEvent => Assert.IsType<GrantSuspended>(domainEvent),
            domainEvent => Assert.IsType<GrantActivated>(domainEvent),
            domainEvent => Assert.IsType<GrantRevoked>(domainEvent));
        Assert.Throws<DomainException>(() => grant.Resume(TestData.GrantMachines, Action(GrantActionType.Resume, "alice")));
    }

    [Fact]
    public void Suspending_and_revoking_need_a_reason()
    {
        var grant = Active();

        Assert.Throws<DomainException>(() => grant.Suspend(TestData.GrantMachines, Action(GrantActionType.Suspend, "alice")));
        Assert.Throws<DomainException>(() => grant.Revoke(TestData.GrantMachines, Action(GrantActionType.Revoke, "alice")));
        Assert.Equal(GrantStatus.Active, grant.Status);
    }

    [Fact]
    public void Illegal_moves_throw()
    {
        var grant = Requested();

        Assert.Throws<DomainException>(() => grant.Suspend(TestData.GrantMachines, Action(GrantActionType.Suspend, "alice", "x")));
        Assert.Throws<DomainException>(() => grant.Resume(TestData.GrantMachines, Action(GrantActionType.Resume, "alice")));
        grant.Reject(TestData.GrantMachines, Action(GrantActionType.Reject, "alice"));
        Assert.Throws<DomainException>(() => grant.Approve(null, TestData.GrantMachines, Action(GrantActionType.Approve, "bob")));
    }

    [Fact]
    public void A_request_must_name_distinct_tools()
    {
        Assert.Throws<DomainException>(() => Grant.Request(_agent, _server, [], null, "Needs it.", Action(GrantActionType.Request, "admin")));
        Assert.Throws<DomainException>(() => Grant.Request(_agent, _server, ["lookup_customer", "lookup_customer"], null, "Needs it.",
            Action(GrantActionType.Request, "admin")));
    }

    [Fact]
    public void A_request_cannot_expire_in_the_past() =>
        Assert.Throws<DomainException>(() => Grant.Request(_agent, _server, ["lookup_customer"], DateTime.UtcNow.AddMinutes(-1), "Needs it.",
            Action(GrantActionType.Request, "admin")));

    private static Grant Requested(string[]? tools = null) =>
        Grant.Request(_agent, _server, tools ?? ["lookup_customer"], DateTime.UtcNow.AddDays(30), "Answers customer questions.",
            Action(GrantActionType.Request, "admin"));

    private static Grant PendingApproval(string[]? tools = null)
    {
        var grant = Requested(tools);
        grant.Approve(null, TestData.GrantMachines, Action(GrantActionType.Approve, "alice"));
        return grant;
    }

    private static Grant Active()
    {
        var grant = PendingApproval();
        grant.Approve(_approvedCrm, TestData.GrantMachines, Action(GrantActionType.Approve, "bob"));
        grant.ClearDomainEvents();
        return grant;
    }

    private static TestAction<GrantActionType> Action(GrantActionType type, string actorId, string? comment = null) => new(type, actorId, comment);
}
