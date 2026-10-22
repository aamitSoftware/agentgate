using Review.Domain.LocalCopies;

namespace Review.Domain.Grants;

public partial class Grant
{
    public static Grant Request(KnownAgent agent, KnownServer server, IReadOnlyCollection<string> tools, DateTime? expiresAt,
        string justification, IAuditableAction<GrantActionType> action)
    {
        if (tools.Count == 0 || tools.Any(string.IsNullOrWhiteSpace))
            throw new DomainException("A grant must name at least one tool.");
        if (tools.Distinct(StringComparer.Ordinal).Count() != tools.Count)
            throw new DomainException("A grant must not name the same tool twice.");
        if (expiresAt <= action.OccurredOn)
            throw new DomainException("A grant cannot expire in the past.");
        if (string.IsNullOrWhiteSpace(justification))
            throw new DomainException("A grant request must say why the agent needs these tools.");

        var grant = new Grant
        {
            AgentId = agent.Id,
            AgentClientId = agent.ClientId,
            ServerId = server.Id,
            ServerName = server.Name,
            Tools = [.. tools.Order(StringComparer.Ordinal)],
            ExpiresAt = expiresAt,
            Justification = justification,
            Status = GrantStatus.Requested,
            CreatedBy = action.ActorId,
            CreatedOn = action.OccurredOn
        };
        grant._auditEntries.Add(GrantAuditEntry.Create(null, GrantStatus.Requested, action));
        return grant;
    }

    /// <summary>True when the next approval activates the grant, so it must be checked against Registry first.</summary>
    public bool AwaitsFinalApproval => Status == GrantStatus.PendingApproval;

    /// <param name="currentServer">For the activating approval: Registry's view of the server right now. Ignored for the first approval.</param>
    public void Approve(CurrentServerState? currentServer, GrantStateMachineFactory stateMachineFactory, IAuditableAction<GrantActionType> action)
    {
        stateMachineFactory.ValidateTransition(Status, action.ActionType);

        if (!AwaitsFinalApproval)
        {
            FirstApproverId = action.ActorId;
            SetStatus(GrantStatus.PendingApproval, stateMachineFactory, action);
            return;
        }

        FourEyes.EnsureSecondApprover(FirstApproverId, action);
        EnsureServerAllowsTools(currentServer ?? throw new DomainException("The server's current state is needed to activate a grant."));
        EnsureNotExpired(action);

        SetStatus(GrantStatus.Active, stateMachineFactory, action);
        AddDomainEvent(new GrantActivated(this, action));
    }

    public void Reject(GrantStateMachineFactory stateMachineFactory, IAuditableAction<GrantActionType> action) =>
        SetStatus(GrantStatus.Rejected, stateMachineFactory, action);

    /// <summary>Stops the grant at the gateway without ending it, e.g. on an anomaly, until a reviewer resumes or revokes it.</summary>
    public void Suspend(GrantStateMachineFactory stateMachineFactory, IAuditableAction<GrantActionType> action)
    {
        stateMachineFactory.ValidateTransition(Status, action.ActionType);
        RequireReason(action);
        SetStatus(GrantStatus.Suspended, stateMachineFactory, action);
        AddDomainEvent(new GrantSuspended(this, action));
    }

    public void Resume(GrantStateMachineFactory stateMachineFactory, IAuditableAction<GrantActionType> action)
    {
        stateMachineFactory.ValidateTransition(Status, action.ActionType);
        EnsureNotExpired(action);

        SetStatus(GrantStatus.Active, stateMachineFactory, action);
        AddDomainEvent(new GrantActivated(this, action));
    }

    public void Revoke(GrantStateMachineFactory stateMachineFactory, IAuditableAction<GrantActionType> action)
    {
        stateMachineFactory.ValidateTransition(Status, action.ActionType);
        RequireReason(action);
        SetStatus(GrantStatus.Revoked, stateMachineFactory, action);
        AddDomainEvent(new GrantRevoked(this, action));
    }

    private void EnsureServerAllowsTools(CurrentServerState server)
    {
        if (!server.IsApproved)
            throw new DomainException($"Server '{ServerName}' is not approved right now; a grant to it cannot be activated.");

        var missing = Tools.Where(tool => !server.PinnedToolNames.Contains(tool)).ToList();
        if (missing.Count > 0)
            throw new DomainException($"Server '{ServerName}' does not offer {string.Join(", ", missing)} in its approved manifest.");
    }

    private void EnsureNotExpired(IAuditableAction action)
    {
        if (ExpiresAt <= action.OccurredOn)
            throw new DomainException("The grant has expired; request a new one.");
    }

    private static void RequireReason(IAuditableAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Comment))
            throw new DomainException("Say why: suspending or revoking a grant must carry a reason.");
    }

    private void SetStatus(GrantStatus newStatus, GrantStateMachineFactory stateMachineFactory, IAuditableAction<GrantActionType> action)
    {
        stateMachineFactory.ValidateTransition(Status, action.ActionType); // before any mutation

        var previousStatus = Status;
        Status = newStatus;
        MarkModified(action);
        _auditEntries.Add(GrantAuditEntry.Create(previousStatus, newStatus, action));
    }
}
