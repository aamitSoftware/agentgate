namespace Review.Domain.LocalCopies;

/// <summary>Registry's MCP server as Review learned it from ManifestSubmitted: enough to address grants to it.</summary>
public sealed class KnownServer : Entity
{
    private KnownServer() { }

    /// <summary>The route key at the gateway (<c>/mcp/{Name}</c>).</summary>
    public string Name { get; private set; } = default!;

    public static KnownServer Create(Guid serverId, string name) => new() { Id = serverId, Name = name };
}
