using System.Text.RegularExpressions;

namespace Registry.Domain.McpServers;

public partial class McpServer
{
    public static McpServer Register(string name, ServerUrl url, ToolManifest manifest, IAuditableAction<ServerActionType> action)
    {
        if (!NamePattern().IsMatch(name))
            throw new DomainException($"'{name}' is not a valid server name: use 2-64 lowercase letters, digits or '-'.");

        var server = new McpServer
        {
            Name = name,
            Url = url,
            Status = ServerStatus.UnderReview,
            PendingManifest = manifest,
            CreatedBy = action.ActorId,
            CreatedOn = action.OccurredOn
        };
        server._auditEntries.Add(ServerAuditEntry.Create(null, ServerStatus.UnderReview, action));
        server.AddDomainEvent(new ManifestSubmitted(server, action));
        return server;
    }

    /// <summary>Pins the manifest under review: from now on it is what agents see and what drift is measured against.</summary>
    /// <param name="reviewedHash">The hash the reviewers approved; must still be the pending manifest.</param>
    public void Approve(string reviewedHash, ServerStateMachineFactory stateMachineFactory, IAuditableAction<ServerActionType> action)
    {
        var manifest = PendingReviewed(reviewedHash);

        SetStatus(ServerStatus.Approved, stateMachineFactory, action);
        PinnedManifest = manifest;
        PendingManifest = null;
        AddDomainEvent(new ServerApproved(this, action));
    }

    /// <summary>The server now advertises something other than what was approved: block it until it is re-reviewed.</summary>
    public void Quarantine(ToolManifest observedManifest, ServerStateMachineFactory stateMachineFactory,
        IAuditableAction<ServerActionType> action)
    {
        if (observedManifest == PinnedManifest)
            throw new DomainException("The observed manifest matches the pinned one; nothing drifted.");

        SetStatus(ServerStatus.Quarantined, stateMachineFactory, action);
        PendingManifest = observedManifest;
        AddDomainEvent(new ServerQuarantined(this, action));
        AddDomainEvent(new ManifestSubmitted(this, action)); // the rug-pull loop: the observed manifest goes to re-review
    }

    /// <summary>The reviewers refused the pending manifest: the server stays blocked for agents.</summary>
    public void Reject(string reviewedHash, ServerStateMachineFactory stateMachineFactory, IAuditableAction<ServerActionType> action)
    {
        var manifest = PendingReviewed(reviewedHash);

        SetStatus(ServerStatus.Rejected, stateMachineFactory, action);
        PendingManifest = null;
        AddDomainEvent(new ServerRejected(this, manifest.Hash, action));
    }

    // insight - a decision is about one exact manifest: if the server advertised something else since,
    // the decision is stale and must not be applied to what is pending now
    private ToolManifest PendingReviewed(string reviewedHash)
    {
        var manifest = PendingManifest ?? throw new DomainException("There is no manifest under review.");
        if (manifest.Hash != reviewedHash)
            throw new DomainException(
                $"The decision was made on manifest {Short(reviewedHash)}, but the pending manifest is {Short(manifest.Hash)}.");
        return manifest;
    }

    private static string Short(string hash) => hash.Length > 12 ? hash[..12] : hash;

    private void SetStatus(ServerStatus newStatus, ServerStateMachineFactory stateMachineFactory,
        IAuditableAction<ServerActionType> action)
    {
        stateMachineFactory.ValidateTransition(Status, action.ActionType); // before any mutation

        var previousStatus = Status;
        Status = newStatus;
        MarkModified(action);
        _auditEntries.Add(ServerAuditEntry.Create(previousStatus, newStatus, action));
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,63}$")]
    private static partial Regex NamePattern();
}
