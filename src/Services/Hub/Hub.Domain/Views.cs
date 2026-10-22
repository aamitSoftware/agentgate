namespace Hub.Domain;

// insight - the Hub decides nothing: every row is a projection of other services' events, shaped for one screen,
// so these are plain data bags with public setters and no invariants

/// <summary>An MCP server as the inventory shows it.</summary>
public sealed class ServerView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Url { get; set; }

    /// <summary>Registry's status name: UnderReview, Approved, Quarantined or Rejected.</summary>
    public string Status { get; set; } = default!;
    public DateTime StatusAsOf { get; set; }

    public string? PinnedHash { get; set; }
    public string? PinnedToolsJson { get; set; }
    public string? PendingHash { get; set; }

    /// <summary>The review waiting for reviewers, if any, and what its scan found.</summary>
    public Guid? OpenReviewId { get; set; }
    public string? OpenReviewRisk { get; set; }

    /// <summary>The risk of the last decided review.</summary>
    public string? LastReviewRisk { get; set; }

    public DateTime FirstSeenOn { get; set; }
}

public sealed class AgentView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string Owner { get; set; } = default!;
    public string Purpose { get; set; } = default!;
    public DateTime RegisteredOn { get; set; }
}

/// <summary>A grant that has been in force at least once: who can (or could) call what.</summary>
public sealed class GrantView
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string AgentClientId { get; set; } = default!;
    public Guid ServerId { get; set; }
    public string ServerName { get; set; } = default!;
    public List<string> Tools { get; set; } = [];
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Active, Suspended or Revoked.</summary>
    public string Status { get; set; } = default!;
    public DateTime StatusAsOf { get; set; }
}

/// <summary>One governance event, filed under every subject it concerns (server, agent, grant).</summary>
public sealed class TimelineEntry
{
    public Guid Id { get; set; }
    public DateTime OccurredOn { get; set; }

    /// <summary>The event's name, e.g. ServerQuarantined.</summary>
    public string Kind { get; set; } = default!;
    public string Summary { get; set; } = default!;

    /// <summary>info, warning or critical: how loudly the console shows it.</summary>
    public string Severity { get; set; } = default!;
    public string? ActorId { get; set; }

    public Guid? ServerId { get; set; }
    public Guid? AgentId { get; set; }
    public Guid? GrantId { get; set; }
    public Guid? ReviewId { get; set; }
}

/// <summary>One gateway decision, as observed (ADR 0005: best effort, not an audit log).</summary>
public sealed class ToolCallView
{
    public Guid Id { get; set; }
    public DateTime OccurredOn { get; set; }
    public string AgentClientId { get; set; } = default!;
    public string ServerName { get; set; } = default!;
    public string Method { get; set; } = default!;
    public string? ToolName { get; set; }

    /// <summary>Allowed or Denied.</summary>
    public string Decision { get; set; } = default!;
    public string? Reason { get; set; }
    public string? TraceId { get; set; }
}
