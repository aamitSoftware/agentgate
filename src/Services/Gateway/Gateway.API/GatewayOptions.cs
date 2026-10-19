using System.ComponentModel.DataAnnotations;

namespace Gateway.API;

public sealed class GatewayOptions
{
    /// <summary>Tokens must be issued for the gateway (aud), not merely by the right realm.</summary>
    [Required]
    public string Audience { get; init; } = default!;

    /// <summary>serverId → upstream MCP endpoint. M2 replaces this with a local copy built from Registry events.</summary>
    public Dictionary<string, UpstreamServer> Servers { get; init; } = [];

    /// <summary>Static grants for M1. M3 replaces this with a local copy built from Review's grant events.</summary>
    public List<GrantRule> Grants { get; init; } = [];
}

public sealed class UpstreamServer
{
    [Required, Url]
    public string Url { get; init; } = default!;
}

public sealed class GrantRule
{
    public string AgentId { get; init; } = default!;
    public string ServerId { get; init; } = default!;
    public List<string> Tools { get; init; } = [];
}
