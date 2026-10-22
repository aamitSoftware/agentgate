namespace Review.Domain.ManifestReviews;

/// <summary>One thing the risk scan noticed about one tool.</summary>
/// <param name="Rule">Stable rule id, e.g. <c>hidden-instructions</c>.</param>
/// <param name="Evidence">The text that triggered the rule, trimmed, so a reviewer can judge it without opening the manifest.</param>
public sealed record RiskFinding(string ToolName, string Rule, RiskLevel Severity, string Evidence);

/// <summary>The scan's verdict: the worst finding decides the level.</summary>
public sealed record RiskAssessment(RiskLevel Level, IReadOnlyList<RiskFinding> Findings)
{
    public static RiskAssessment From(IReadOnlyList<RiskFinding> findings) =>
        new(findings.Count == 0 ? RiskLevel.Low : findings.Max(finding => finding.Severity), findings);
}
