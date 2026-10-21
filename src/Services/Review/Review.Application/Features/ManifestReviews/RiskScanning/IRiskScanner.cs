namespace Review.Application.Features.ManifestReviews.RiskScanning;

/// <param name="ToolsJson">The manifest to scan: canonical JSON array of MCP Tool objects.</param>
/// <param name="PreviousToolsJson">The pinned manifest it would replace, to report what changed; null for a new server.</param>
public sealed record ManifestToScan(string ServerName, string ToolsJson, string? PreviousToolsJson);

/// <summary>Assesses what a manifest would tell an agent. Heuristic by default; a model-based scanner comes in M5.</summary>
public interface IRiskScanner
{
    Task<RiskAssessment> ScanAsync(ManifestToScan manifest, CancellationToken ct);
}
