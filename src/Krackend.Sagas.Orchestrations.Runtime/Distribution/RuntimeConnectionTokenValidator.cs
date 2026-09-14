using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Microsoft.Extensions.Caching.Distributed;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Default Runtime bearer token validator.
/// </summary>
public sealed class RuntimeConnectionTokenValidator : IRuntimeConnectionTokenValidator
{
    private readonly IRuntimeDesignNodeRepository _repository;
    private readonly ITokenHashService _tokenHashService;
    private readonly IConnectionScopeFormatter _scopeFormatter;
    private readonly ConnectionTokenCacheKeyBuilder _cacheKeyBuilder;
    private readonly IDistributedCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeConnectionTokenValidator"/> class.
    /// </summary>
    public RuntimeConnectionTokenValidator(
        IRuntimeDesignNodeRepository repository,
        ITokenHashService tokenHashService,
        IConnectionScopeFormatter scopeFormatter,
        ConnectionTokenCacheKeyBuilder cacheKeyBuilder,
        IDistributedCache cache)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _tokenHashService = tokenHashService ?? throw new ArgumentNullException(nameof(tokenHashService));
        _scopeFormatter = scopeFormatter ?? throw new ArgumentNullException(nameof(scopeFormatter));
        _cacheKeyBuilder = cacheKeyBuilder ?? throw new ArgumentNullException(nameof(cacheKeyBuilder));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public async Task<ConnectionTokenValidationResult> ValidateAsync(
        string token,
        IReadOnlyCollection<ArtifactDeliveryScope> requiredScopes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ConnectionTokenValidationResult.Failure("Bearer token is required.");
        }

        var tokenHash = _tokenHashService.HashToken(token);
        var validationKey = _cacheKeyBuilder.BuildValidationKey("runtime", tokenHash);
        var cachedValidation = await _cache.GetStringAsync(validationKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedValidation))
        {
            var cachedPrincipal = JsonSerializer.Deserialize<ConnectionTokenPrincipal>(cachedValidation, JsonOptions());
            if (cachedPrincipal is not null && HasRequiredScopes(cachedPrincipal.Scopes, requiredScopes))
            {
                return ConnectionTokenValidationResult.Success(cachedPrincipal);
            }
        }

        var tokenJson = await _cache.GetStringAsync(
            _cacheKeyBuilder.BuildIssuedTokenKey("runtime", tokenHash),
            cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenJson))
        {
            return ConnectionTokenValidationResult.Failure("Bearer token is invalid or expired.");
        }

        var entry = JsonSerializer.Deserialize<ConnectionTokenCacheEntry>(tokenJson, JsonOptions());
        if (entry is null || entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return ConnectionTokenValidationResult.Failure("Bearer token is invalid or expired.");
        }

        var node = await _repository.GetByIdAsync(new Id(Ulid.Parse(entry.NodeId)), cancellationToken);
        if (node is null ||
            node.Status != RuntimeDesignNodeStatus.Enabled ||
            !node.IsEnabled ||
            node.InboundCredentialStatus != ConnectionCredentialStatus.Active)
        {
            return ConnectionTokenValidationResult.Failure("Bearer token credential is not active.");
        }

        if (!string.Equals(node.InboundClientId, entry.ClientId, StringComparison.Ordinal) ||
            !string.Equals(node.InboundKeyId, entry.KeyId, StringComparison.Ordinal))
        {
            return ConnectionTokenValidationResult.Failure("Bearer token credential does not match the current node key.");
        }

        var scopes = _scopeFormatter.ParseMany(entry.Scopes);
        if (!HasRequiredScopes(scopes, requiredScopes))
        {
            return ConnectionTokenValidationResult.Failure("Bearer token does not include the required scope.");
        }

        var principal = new ConnectionTokenPrincipal
        {
            NodeId = entry.NodeId,
            NodeKey = entry.NodeKey,
            ClientId = entry.ClientId,
            KeyId = entry.KeyId,
            Scopes = scopes
        };

        var validationTtl = TimeSpan.FromSeconds(Math.Max(1, node.TokenValidationCacheTtlSeconds));
        var remaining = entry.ExpiresAtUtc - DateTime.UtcNow;
        await _cache.SetStringAsync(
            validationKey,
            JsonSerializer.Serialize(principal, JsonOptions()),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining < validationTtl ? remaining : validationTtl },
            cancellationToken);

        return ConnectionTokenValidationResult.Success(principal);
    }

    private static bool HasRequiredScopes(
        IReadOnlyCollection<ArtifactDeliveryScope> grantedScopes,
        IReadOnlyCollection<ArtifactDeliveryScope> requiredScopes)
    {
        if (requiredScopes is null || requiredScopes.Count == 0)
        {
            return true;
        }

        return requiredScopes.All(grantedScopes.Contains);
    }

    private static JsonSerializerOptions JsonOptions()
        => new(JsonSerializerDefaults.Web);
}
