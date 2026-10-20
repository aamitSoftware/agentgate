using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AgentGate.IntegrationEvents.Registry;

namespace Gateway.API.Features.Catalog;

public sealed record PinnedTool(string Name, JsonElement Definition);

/// <summary>An approved server as the gateway knows it: where it lives and exactly which tools were approved.</summary>
public sealed record PinnedServer(Guid ServerId, string Url, string ManifestHash, IReadOnlyList<PinnedTool> Tools,
    bool Quarantined, DateTime AsOf);

/// <summary>
/// The gateway's local copy of Registry's approved servers, built only from events.
/// The request path reads it and never calls Registry.
/// </summary>
// ponytail: in memory, so a restarted gateway knows no servers and denies everything (fail closed) until
// Registry re-announces them; M3 adds a bootstrap read from Registry. One replica only: replicas would share
// one queue and each see only part of the events.
public sealed class ServerCatalog
{
    private readonly ConcurrentDictionary<string, PinnedServer> _servers = new(StringComparer.Ordinal);

    public bool TryGet(string name, [NotNullWhen(true)] out PinnedServer? server) => _servers.TryGetValue(name, out server);

    public void Pin(ServerApproved approved) =>
        Apply(approved.Name, approved.OccurredOn, _ => new PinnedServer(approved.ServerId, approved.Url, approved.ManifestHash,
            ParseTools(approved.ToolsJson), Quarantined: false, approved.OccurredOn));

    public void Quarantine(ServerQuarantined quarantined) =>
        Apply(quarantined.Name, quarantined.OccurredOn, current => current is null
            // never seen approved (e.g. after a restart): remember the block so an older approval can't lift it
            ? new PinnedServer(quarantined.ServerId, Url: "", quarantined.PinnedHash, [], Quarantined: true, quarantined.OccurredOn)
            : current with { Quarantined = true, AsOf = quarantined.OccurredOn });

    // insight - overwrite, never increment: a redelivered event converges to the same state (no inbox needed),
    // and an event older than what is already applied is dropped (approval and quarantine use separate queues)
    private void Apply(string name, DateTime occurredOn, Func<PinnedServer?, PinnedServer> next) =>
        _servers.AddOrUpdate(name, _ => next(null), (_, current) => occurredOn >= current.AsOf ? next(current) : current);

    private static List<PinnedTool> ParseTools(string toolsJson)
    {
        using var document = JsonDocument.Parse(toolsJson);
        return [.. document.RootElement.EnumerateArray()
            .Select(tool => new PinnedTool(tool.GetProperty("name").GetString()!, tool.Clone()))];
    }
}
