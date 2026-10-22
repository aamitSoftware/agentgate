using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Review.API.RegistryClient;

/// <summary>Review's own Keycloak client: how it authenticates to Registry for GetServerSnapshot (ADR 0004).</summary>
public sealed class ServiceIdentityOptions
{
    [Required] public string TokenEndpoint { get; init; } = default!;
    [Required] public string ClientId { get; init; } = default!;
    [Required] public string ClientSecret { get; init; } = default!;
}

public sealed class RegistryClientOptions
{
    /// <summary>Registry's HTTP/2 endpoint; <c>_grpc.registry</c> resolves the named endpoint through service discovery.</summary>
    [Required] public string Address { get; init; } = default!;
}

/// <summary>Client-credentials tokens for Review itself, cached until shortly before they expire.</summary>
// singleton: the cache must outlive the pooled HttpClient handlers, so it creates a client per refresh instead of holding one
public sealed class ServiceTokenProvider(IHttpClientFactory httpClientFactory, IOptions<ServiceIdentityOptions> options, TimeProvider timeProvider)
{
    public const string HttpClientName = "keycloak-token";

    private static readonly TimeSpan _refreshMargin = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private (string AccessToken, DateTimeOffset RefreshAt)? _cached;

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_cached is { } cached && timeProvider.GetUtcNow() < cached.RefreshAt)
            return cached.AccessToken;

        await _refreshLock.WaitAsync(ct);
        try
        {
            if (_cached is { } refreshed && timeProvider.GetUtcNow() < refreshed.RefreshAt)
                return refreshed.AccessToken; // another caller refreshed while this one waited

            var identity = options.Value;
            using var httpClient = httpClientFactory.CreateClient(HttpClientName);
            using var response = await httpClient.PostAsync(identity.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = identity.ClientId,
                ["client_secret"] = identity.ClientSecret
            }), ct);
            response.EnsureSuccessStatusCode();

            var token = (await response.Content.ReadFromJsonAsync<TokenResponse>(ct))!;
            _cached = (token.AccessToken, timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn) - _refreshMargin);
            return token.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>Puts Review's own token on outgoing calls to Registry (never a user's token: the caller is the service).</summary>
public sealed class ServiceTokenHandler(ServiceTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }
}
