namespace Review.Domain.ManifestReviews;

public partial class ManifestReview
{
    public static ManifestReview Open(Guid serverId, string serverName, string manifestHash, string toolsJson,
        string? previousHash, string? previousToolsJson, string submissionReason, RiskAssessment assessment,
        IAuditableAction<ReviewActionType> action)
    {
        var review = new ManifestReview
        {
            ServerId = serverId,
            ServerName = serverName,
            ManifestHash = manifestHash,
            ToolsJson = toolsJson,
            PreviousHash = previousHash,
            PreviousToolsJson = previousToolsJson,
            SubmissionReason = submissionReason,
            RiskLevel = assessment.Level,
            Findings = assessment.Findings,
            Status = ReviewStatus.Open,
            CreatedBy = action.ActorId,
            CreatedOn = action.OccurredOn
        };
        review._auditEntries.Add(ReviewAuditEntry.Create(null, ReviewStatus.Open, action));
        review.AddDomainEvent(new ManifestReviewOpened(review, action));
        return review;
    }

    /// <summary>True when the next approval completes the review, so it must be checked against Registry first.</summary>
    public bool AwaitsFinalApproval => Status == ReviewStatus.PartiallyApproved;

    /// <param name="currentPendingHash">
    /// For the final approval: the manifest Registry has pending right now (from GetServerSnapshot). The review is only
    /// valid for the exact manifest it scanned. Ignored for the first approval.
    /// </param>
    public void Approve(string? currentPendingHash, ReviewStateMachineFactory stateMachineFactory, IAuditableAction<ReviewActionType> action)
    {
        stateMachineFactory.ValidateTransition(Status, action.ActionType);

        // insight - a high-risk manifest can still be approved (a scanner can be wrong), but never silently:
        // every approver has to put their reasoning on the record
        if (RiskLevel == RiskLevel.High && string.IsNullOrWhiteSpace(action.Comment))
            throw new DomainException("This manifest was scanned as high risk: each approval must explain why it is acceptable.");

        if (!AwaitsFinalApproval)
        {
            FirstApproverId = action.ActorId;
            SetStatus(ReviewStatus.PartiallyApproved, stateMachineFactory, action);
            return;
        }

        FourEyes.EnsureSecondApprover(FirstApproverId, action);
        if (currentPendingHash != ManifestHash)
            throw new DomainException(currentPendingHash is null or ""
                ? $"Server '{ServerName}' has no manifest pending review any more; this review is outdated."
                : $"Server '{ServerName}' now has manifest {currentPendingHash[..Math.Min(12, currentPendingHash.Length)]} pending; " +
                  $"this review of {ManifestHash[..12]} is outdated.");

        SetStatus(ReviewStatus.Approved, stateMachineFactory, action);
        AddDomainEvent(new ManifestReviewCompleted(this, [FirstApproverId!, action.ActorId], action));
    }

    public void Reject(ReviewStateMachineFactory stateMachineFactory, IAuditableAction<ReviewActionType> action)
    {
        stateMachineFactory.ValidateTransition(Status, action.ActionType);
        if (string.IsNullOrWhiteSpace(action.Comment))
            throw new DomainException("A rejection must say why.");

        SetStatus(ReviewStatus.Rejected, stateMachineFactory, action);
        AddDomainEvent(new ManifestReviewCompleted(this, [action.ActorId], action));
    }

    /// <summary>A newer manifest was submitted for the same server: this one can no longer be decided.</summary>
    public void Supersede(ReviewStateMachineFactory stateMachineFactory, IAuditableAction<ReviewActionType> action) =>
        SetStatus(ReviewStatus.Superseded, stateMachineFactory, action);

    private void SetStatus(ReviewStatus newStatus, ReviewStateMachineFactory stateMachineFactory, IAuditableAction<ReviewActionType> action)
    {
        stateMachineFactory.ValidateTransition(Status, action.ActionType); // before any mutation

        var previousStatus = Status;
        Status = newStatus;
        MarkModified(action);
        _auditEntries.Add(ReviewAuditEntry.Create(previousStatus, newStatus, action));
    }
}
