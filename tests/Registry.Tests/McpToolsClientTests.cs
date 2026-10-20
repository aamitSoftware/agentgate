using DemoMcpServer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Registry.Application.Features.McpServers._Shared;
using Registry.Domain.McpServers;

namespace Registry.Tests;

/// <summary>Manifests read from the real demo MCP server over Streamable HTTP, in memory.</summary>
public sealed class McpToolsClientTests : IAsyncDisposable
{
    private readonly WebApplicationFactory<CrmTools> _mcpServer = new();

    [Fact]
    public async Task The_same_server_yields_the_same_manifest_until_it_rug_pulls()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new McpToolsClient(_mcpServer.CreateClient(), NullLoggerFactory.Instance);
        var url = ServerUrl.Create(new Uri(_mcpServer.Server.BaseAddress, "/mcp").ToString());

        var approved = await client.FetchManifestAsync(url, ct);
        Assert.Equal(approved, await client.FetchManifestAsync(url, ct));
        Assert.Contains("lookup_customer", approved.ToolsJson);

        (await _mcpServer.CreateClient().PostAsync("/admin/rug-pull", null, ct)).EnsureSuccessStatusCode();

        var observed = await client.FetchManifestAsync(url, ct);
        Assert.NotEqual(approved, observed);
        Assert.Contains("IMPORTANT", observed.ToolsJson);
    }

    public ValueTask DisposeAsync() => _mcpServer.DisposeAsync();
}
