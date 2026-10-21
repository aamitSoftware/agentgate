namespace AgentGate.Abstractions;

/// <summary>Actor ids for changes no human made. Audit entries name them explicitly; never a null user.</summary>
public static class SystemActors
{
    public const string DriftDetector = "system:drift-detector";
    public const string AnomalyDetector = "system:anomaly-detector";

    /// <summary>Opens a review when Registry submits a manifest, and supersedes the reviews it replaces.</summary>
    public const string ReviewIntake = "system:review-intake";
}
