using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;

namespace Gateway.API.Features.McpProxy;

/// <summary>The parts of an MCP request the gateway decides on.</summary>
public sealed record McpRequest(JsonElement? Id, string Method, string? Name);

public sealed record McpRejection(int StatusCode, int ErrorCode, string Message, JsonElement? Id = null);

/// <summary>
/// Validates an MCP Streamable HTTP POST (spec 2026-07-28) before any policy decision.
/// Requests only: responses are never parsed, so the gateway stays out of the streaming path.
/// </summary>
public static class McpRequestInspector
{
    public const string SupportedProtocolVersion = "2026-07-28";

    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int HeaderMismatch = -32020;

    private const string ProtocolVersionHeader = "MCP-Protocol-Version";
    private const string MethodHeader = "Mcp-Method";
    private const string NameHeader = "Mcp-Name";
    private const string ProtocolVersionMetaKey = "io.modelcontextprotocol/protocolVersion";

    public static bool TryInspect(IHeaderDictionary headers, ReadOnlyMemory<byte> body,
        [NotNullWhen(true)] out McpRequest? request, [NotNullWhen(false)] out McpRejection? rejection)
    {
        rejection = Inspect(headers, body, out request);
        return rejection is null;
    }

    private static McpRejection? Inspect(IHeaderDictionary headers, ReadOnlyMemory<byte> body, out McpRequest? request)
    {
        request = null;

        // insight - routing and policy must never trust a header the body disagrees with:
        // otherwise a header-routed intermediary and the server can act on two different requests
        var protocolVersion = headers[ProtocolVersionHeader].ToString();
        if (protocolVersion.Length == 0)
            return new(StatusCodes.Status400BadRequest, HeaderMismatch, $"Missing {ProtocolVersionHeader} header.");
        if (protocolVersion != SupportedProtocolVersion)
            return new(StatusCodes.Status400BadRequest, InvalidRequest,
                $"Unsupported protocol version '{protocolVersion}'; supported: {SupportedProtocolVersion}.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return new(StatusCodes.Status400BadRequest, ParseError, "Body is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new(StatusCodes.Status400BadRequest, InvalidRequest, "Body must be a single JSON-RPC request.");

            if (!root.TryGetProperty("id", out var idElement))
                return new(StatusCodes.Status400BadRequest, InvalidRequest, "Notifications are not accepted on this transport.");
            JsonElement? id = idElement.Clone();

            if (!root.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
                return new(StatusCodes.Status400BadRequest, InvalidRequest, "Missing 'method'.", id);
            var method = methodElement.GetString()!;

            if (headers[MethodHeader].ToString() != method)
                return new(StatusCodes.Status400BadRequest, HeaderMismatch, $"{MethodHeader} header does not match the body method.", id);

            root.TryGetProperty("params", out var parameters);
            var hasParameters = parameters.ValueKind == JsonValueKind.Object;

            var bodyVersion = hasParameters
                && parameters.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object
                && meta.TryGetProperty(ProtocolVersionMetaKey, out var version) && version.ValueKind == JsonValueKind.String
                    ? version.GetString()
                    : null;
            if (bodyVersion != protocolVersion)
                return new(StatusCodes.Status400BadRequest, HeaderMismatch, $"{ProtocolVersionHeader} header does not match the body _meta.", id);

            string? name = null;
            var nameField = method switch
            {
                "tools/call" or "prompts/get" => "name",
                "resources/read" => "uri",
                _ => null
            };
            if (nameField is not null)
            {
                name = hasParameters && parameters.TryGetProperty(nameField, out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString()
                    : null;
                if (name is null)
                    return new(StatusCodes.Status400BadRequest, InvalidRequest, $"Missing params.{nameField}.", id);

                if (DecodeHeader(headers[NameHeader].ToString()) != name)
                    return new(StatusCodes.Status400BadRequest, HeaderMismatch, $"{NameHeader} header does not match params.{nameField}.", id);
            }

            request = new McpRequest(id, method, name);
            return null;
        }
    }

    // values outside the header-safe set arrive as =?base64?...?= (reuses the SDK's decoder)
    private static string? DecodeHeader(string value)
    {
        if (value.Length == 0)
            return null;
        try
        {
            return McpHeaderEncoder.DecodeValue(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
