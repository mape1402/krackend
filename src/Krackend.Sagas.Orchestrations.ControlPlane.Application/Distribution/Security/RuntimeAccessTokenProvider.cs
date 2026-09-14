using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Microsoft.Extensions.Caching.Distributed;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Default cached token provider for Design calls into Runtime.
/// </summary>
public sealed class RuntimeAccessTokenProvider : IRuntimeAccessTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly IControlPlaneRuntimeNodeSecretProtector _secretProtector;
    private readonly IConnectionScopeFormatter _scopeFormatter;
    private readonly ConnectionTokenCacheKeyBuilder _cacheKeyBuilder;
    private readonly IDistributedCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeAccessTokenProvider"/> class.
    /// </summary>
    public RuntimeAccessTokenProvider(
        HttpClient httpClient,
        IControlPlaneRuntimeNodeSecretProtector secretProtector,
        IConnectionScopeFormatter scopeFormatter,
        ConnectionTokenCacheKeyBuilder cacheKeyBuilder,
        IDistributedCache cache)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
        _scopeFormatter = scopeFormatter ?? throw new ArgumentNullException(nameof(scopeFormatter));
        _cacheKeyBuilder = cacheKeyBuilder ?? throw new ArgumentNullException(nameof(cacheKeyBuilder));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public async Task AttachTokenAsync(
        HttpRequestMessage request,
        RuntimeNode node,
        IReadOnlyCollection<ArtifactDeliveryScope> scopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(node);

        var scopeValue = _scopeFormatter.FormatMany(scopes);
        var token = await GetTokenAsync(node, scopeValue, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> GetTokenAsync(
        RuntimeNode node,
        string scopeValue,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(node.ProtectedOutboundSecret) ||
            string.IsNullOrWhiteSpace(node.OutboundClientId))
        {
            throw new InvalidOperationException($"Runtime node '{node.Name}' does not have outbound credentials.");
        }

        var cacheKey = _cacheKeyBuilder.BuildConsumerTokenKey("design", node.Id.ToString(), scopeValue);
        var cachedJson = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            var cached = JsonSerializer.Deserialize<CachedConnectionToken>(cachedJson, JsonOptions());
            if (cached is not null && cached.ExpiresAtUtc > DateTime.UtcNow.AddSeconds(node.TokenRefreshSkewSeconds))
            {
                return cached.AccessToken;
            }
        }

        var secret = _secretProtector.Unprotect(node.ProtectedOutboundSecret);
        var body = JsonSerializer.Serialize(new ConnectionTokenRequest
        {
            GrantType = "client_credentials",
            ClientId = node.OutboundClientId,
            ClientSecret = secret,
            Scope = scopeValue
        }, JsonOptions());

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, BuildTokenUri(node))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        using var response = await _httpClient.SendAsync(tokenRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var token = JsonSerializer.Deserialize<ConnectionTokenResponse>(responseBody, JsonOptions())
            ?? throw new InvalidOperationException("Runtime returned an empty token response.");

        var ttl = token.ExpiresAtUtc - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Runtime returned an expired token.");
        }

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(new CachedConnectionToken
            {
                AccessToken = token.AccessToken,
                ExpiresAtUtc = token.ExpiresAtUtc,
                KeyId = token.KeyId,
                Scope = token.Scope
            }, JsonOptions()),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken);

        return token.AccessToken;
    }

    private static Uri BuildTokenUri(RuntimeNode node)
        => new($"{node.EndpointBaseUri.TrimEnd('/')}/runtime/distribution/connect/token", UriKind.Absolute);

    private static JsonSerializerOptions JsonOptions()
        => new(JsonSerializerDefaults.Web);
}
