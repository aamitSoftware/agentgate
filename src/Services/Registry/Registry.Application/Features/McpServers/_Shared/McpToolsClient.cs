using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Registry.Application.Features.McpServers._Shared;

/// <summary>Reads what an MCP server advertises right now (tools/list) as a manifest.</summary>
public sealed class McpToolsClient(HttpClient httpClient, ILoggerFactory loggerFactory)
{
    public const string ProtocolVersion = "2026-07-28";

    public async Task<ToolManifest> FetchManifestAsync(ServerUrl url, CancellationToken ct)
    {
        try
        {
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions { Endpoint = new Uri(url.Value), TransportMode = HttpTransportMode.StreamableHttp },
                httpClient, loggerFactory, ownsHttpClient: false);
            await using var client = await McpClient.CreateAsync(transport, new McpClientOptions { ProtocolVersion = ProtocolVersion },
                loggerFactory, ct);

            var tools = await client.ListToolsAsync(cancellationToken: ct); // follows pagination
            return ToolManifest.Create(JsonSerializer.Serialize<IList<Tool>>([.. tools.Select(tool => tool.ProtocolTool)],
                McpJsonUtilities.DefaultOptions));
        }
        catch (Exception exception) when (exception is HttpRequestException or McpException or JsonException)
        {
            throw new BadRequestException($"Could not read tools/list from '{url}': {exception.Message}", exception);
        }
    }
}
