using System.Text;
using Gateway.API.Features.McpProxy;
using Microsoft.AspNetCore.Http;

namespace Gateway.Tests;

public class McpRequestInspectorTests
{
    private const string Version = McpRequestInspector.SupportedProtocolVersion;

    [Fact]
    public void Accepts_a_consistent_tools_call()
    {
        var accepted = Inspect(Headers("tools/call", "lookup_customer"), Body("tools/call", "lookup_customer"), out var request, out _);

        Assert.True(accepted);
        Assert.Equal(("tools/call", "lookup_customer"), (request!.Method, request.Name));
    }

    [Fact]
    public void Accepts_a_base64_encoded_name_header()
    {
        var encoded = $"=?base64?{Convert.ToBase64String(Encoding.UTF8.GetBytes("lookup_customer"))}?=";

        Assert.True(Inspect(Headers("tools/call", encoded), Body("tools/call", "lookup_customer"), out _, out _));
    }

    public static TheoryData<string, IHeaderDictionary, string, int> Rejections => new()
    {
        { "method header differs from body", Headers("tools/list"), Body("tools/call", "lookup_customer"), McpRequestInspector.HeaderMismatch },
        { "name header smuggles another tool", Headers("tools/call", "lookup_customer"), Body("tools/call", "export_all_customers"), McpRequestInspector.HeaderMismatch },
        { "name header missing", Headers("tools/call"), Body("tools/call", "lookup_customer"), McpRequestInspector.HeaderMismatch },
        { "protocol header missing", Headers("tools/call", "lookup_customer", version: null), Body("tools/call", "lookup_customer"), McpRequestInspector.HeaderMismatch },
        { "body _meta version differs", Headers("tools/call", "lookup_customer"), Body("tools/call", "lookup_customer", metaVersion: "2025-11-25"), McpRequestInspector.HeaderMismatch },
        { "unsupported protocol version", Headers("tools/call", "lookup_customer", version: "2025-11-25"), Body("tools/call", "lookup_customer"), McpRequestInspector.InvalidRequest },
        { "batch array", Headers("tools/call", "lookup_customer"), "[]", McpRequestInspector.InvalidRequest },
        { "notification (no id)", Headers("tools/call", "lookup_customer"), Body("tools/call", "lookup_customer", includeId: false), McpRequestInspector.InvalidRequest },
        { "not json", Headers("tools/call", "lookup_customer"), "{not json", McpRequestInspector.ParseError },
    };

    [Theory]
    [MemberData(nameof(Rejections))]
    public void Rejects_inconsistent_or_malformed_requests(string scenario, IHeaderDictionary headers, string body, int expectedCode)
    {
        var accepted = Inspect(headers, body, out _, out var rejection);

        Assert.False(accepted, scenario);
        Assert.Equal(StatusCodes.Status400BadRequest, rejection!.StatusCode);
        Assert.Equal(expectedCode, rejection.ErrorCode);
    }

    private static bool Inspect(IHeaderDictionary headers, string body, out McpRequest? request, out McpRejection? rejection) =>
        McpRequestInspector.TryInspect(headers, Encoding.UTF8.GetBytes(body), out request, out rejection);

    internal static IHeaderDictionary Headers(string method, string? name = null, string? version = Version)
    {
        var headers = new HeaderDictionary { ["Mcp-Method"] = method };
        if (name is not null)
            headers["Mcp-Name"] = name;
        if (version is not null)
            headers["MCP-Protocol-Version"] = version;
        return headers;
    }

    internal static string Body(string method, string? name = null, string metaVersion = Version, bool includeId = true) =>
        $$"""
        {
          "jsonrpc": "2.0",
          {{(includeId ? "\"id\": 1," : "")}}
          "method": "{{method}}",
          "params": {
            {{(name is null ? "" : $"\"name\": \"{name}\", \"arguments\": {{}},")}}
            "_meta": { "io.modelcontextprotocol/protocolVersion": "{{metaVersion}}" }
          }
        }
        """;
}
