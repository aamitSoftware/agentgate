namespace Review.Domain.ManifestReviews;

/// <summary>
/// The security review of one exact manifest of one MCP server: what the risk scan found, and the reviewers'
/// decision. Approval takes two different reviewers; one rejection is enough.
/// </summary>
public partial class ManifestReview : AggregateRoot
{
    private ManifestReview() { }

    public Guid ServerId { get; private init; }
    public string ServerName { get; private init; } = default!;

    /// <summary>The manifest under review, as Registry submitted it (canonical JSON, SHA-256).</summary>
    public string ManifestHash { get; private init; } = default!;
    public string ToolsJson { get; private init; } = default!;

    /// <summary>The pinned manifest this one would replace; null for a new server.</summary>
    public string? PreviousHash { get; private init; }
    public string? PreviousToolsJson { get; private init; }

    /// <summary>Why the manifest was submitted, e.g. registration or the drift detector's note.</summary>
    public string SubmissionReason { get; private init; } = default!;

    public RiskLevel RiskLevel { get; private init; }
    public IReadOnlyList<RiskFinding> Findings { get; private init; } = [];

    public ReviewStatus Status { get; private set; }
    public string? FirstApproverId { get; private set; }

    private readonly List<ReviewAuditEntry> _auditEntries = [];
    public IReadOnlyList<ReviewAuditEntry> AuditEntries => _auditEntries.AsReadOnly();
}
