using Microsoft.Extensions.Options;

namespace Gateway.API.Features.McpProxy;

/// <summary>Which agent may call which tool on which server. Deny unless listed.</summary>
// ponytail: static grants from configuration for M1; M3 rebuilds this from GrantActivated/Suspended/Revoked events
public sealed class GrantTable(IOptions<GatewayOptions> options)
{
    private readonly HashSet<(string AgentId, string ServerId, string Tool)> _grants =
        options.Value.Grants
            .SelectMany(grant => grant.Tools.Select(tool => (grant.AgentId, grant.ServerId, tool)))
            .ToHashSet();

    public bool IsGranted(string agentId, string serverId, string tool) => _grants.Contains((agentId, serverId, tool));
}
