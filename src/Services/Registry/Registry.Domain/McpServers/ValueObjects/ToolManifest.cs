using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Registry.Domain.McpServers;

/// <summary>
/// The tools an MCP server advertises, in canonical form, identified by a SHA-256 hash.
/// Two manifests are equal when everything an agent is told about the tools is equal,
/// regardless of tool order, JSON key order or whitespace.
/// </summary>
public sealed class ToolManifest : ValueObject
{
    // insight - only what reaches the model is pinned: a changed description or schema is a rug pull,
    // a changed icon or _meta is presentation
    private static readonly string[] _pinnedFields =
        ["annotations", "description", "inputSchema", "name", "outputSchema", "title"]; // ordinal order

    private ToolManifest(string toolsJson, string hash, IReadOnlyList<string> toolNames)
    {
        ToolsJson = toolsJson;
        Hash = hash;
        ToolNames = toolNames;
    }

    /// <summary>Canonical JSON array of tools: sorted by name, object keys sorted, compact.</summary>
    public string ToolsJson { get; }

    /// <summary>Lowercase hex SHA-256 of <see cref="ToolsJson"/>.</summary>
    public string Hash { get; }

    /// <summary>Tool names in canonical (ordinal) order.</summary>
    public IReadOnlyList<string> ToolNames { get; }

    /// <param name="toolsJson">A JSON array of MCP Tool objects, as returned in a tools/list result.</param>
    public static ToolManifest Create(string toolsJson)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(toolsJson);
        }
        catch (JsonException exception)
        {
            throw new DomainException("Tool manifest is not valid JSON.", exception);
        }

        if (root is not JsonArray tools)
            throw new DomainException("Tool manifest must be a JSON array of tools.");

        var canonicalTools = tools.Select(CanonicalTool).ToList();
        var names = canonicalTools.Select(tool => (string)tool["name"]!).ToList();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count)
            throw new DomainException("Tool manifest contains duplicate tool names.");

        var canonicalJson = new JsonArray([.. canonicalTools.OrderBy(tool => (string)tool["name"]!, StringComparer.Ordinal)])
            .ToJsonString();
        return new ToolManifest(canonicalJson, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))),
            [.. names.Order(StringComparer.Ordinal)]);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Hash;
    }

    public override string ToString() => Hash;

    private static JsonObject CanonicalTool(JsonNode? node)
    {
        if (node is not JsonObject tool || tool["name"] is not JsonValue name || name.GetValueKind() != JsonValueKind.String)
            throw new DomainException("Every tool must be an object with a string 'name'.");

        var canonical = new JsonObject();
        foreach (var field in _pinnedFields)
            if (tool.TryGetPropertyValue(field, out var value) && value is not null)
                canonical[field] = Canonical(value);
        return canonical;
    }

    private static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject jsonObject => new JsonObject(jsonObject
            .OrderBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => KeyValuePair.Create(property.Key, Canonical(property.Value)))),
        JsonArray jsonArray => new JsonArray([.. jsonArray.Select(Canonical)]), // array order is meaningful (e.g. enum, required)
        _ => node?.DeepClone()
    };
}
