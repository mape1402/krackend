using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Microsoft.Extensions.Caching.Distributed;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Default Design token issuer for Runtime initiated artifact distribution calls.
/// </summary>
public sealed class ControlPlaneConnectionTokenIssuer : IControlPlaneConnectionTokenIssuer
{
    private readonly IRuntimeNodeRepository _repository;
    private readonly IConnectionSecretHasher _secretHasher;
    private readonly IConnectionSecretGenerator _secretGenerator;
    private readonly ITokenHashService _tokenHashService;
    private readonly IConnectionScopeFormatter _scopeFormatter;
    private readonly ConnectionTokenCacheKeyBuilder _cacheKeyBuilder;
    private readonly IDistributedCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlPlaneConnectionTokenIssuer"/> class.
    /// </summary>
    public ControlPlaneConnectionTokenIssuer(
        IRuntimeNodeRepository repository,
        IConnectionSecretHasher secretHasher,
        IConnectionSecretGenerator secretGenerator,
        ITokenHashService tokenHashService,
        IConnectionScopeFormatter scopeFormatter,
        ConnectionTokenCacheKeyBuilder cacheKeyBuilder,
        IDistributedCache cache)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _secretHasher = secretHasher ?? throw new ArgumentNullException(nameof(secretHasher));
        _secretGenerator = secretGenerator ?? throw new ArgumentNullException(nameof(secretGenerator));
        _tokenHashService = tokenHashService ?? throw new ArgumentNullException(nameof(tokenHashService));
        _scopeFormatter = scopeFormatter ?? throw new ArgumentNullException(nameof(scopeFormatter));
        _cacheKeyBuilder = cacheKeyBuilder ?? throw new ArgumentNullException(nameof(cacheKeyBuilder));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public async Task<ConnectionTokenResponse> IssueAsync(
        ConnectionTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.GrantType, "client_credentials", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only client_credentials grant type is supported.");
        }

        var node = await _repository.GetByInboundClientId(request.ClientId, cancellationToken);
        if (node is null)
        {
            throw new InvalidOperationException("Client credential is not registered.");
        }

        var now = DateTime.UtcNow;
        try
        {
            EnsureNodeCanIssueToken(node);
            EnsureRequestedScopesAreAllowed(request.Scope, node.InboundAllowedScopes);

            if (!_secretHasher.VerifySecret(request.ClientSecret, node.InboundSecretHash))
            {
                throw new InvalidOperationException("Client credential secret is invalid.");
            }

            var token = _secretGenerator.GenerateToken();
            var expiresIn = Math.Max(60, node.AccessTokenTtlSeconds);
            var expiresAtUtc = now.AddSeconds(expiresIn);
            var entry = new ConnectionTokenCacheEntry
            {
                NodeId = node.Id.ToString(),
                NodeKey = node.Code,
                ClientId = node.InboundClientId,
                KeyId = node.InboundKeyId,
                Scopes = request.Scope,
                ExpiresAtUtc = expiresAtUtc
            };

            var tokenHash = _tokenHashService.HashToken(token);
            await _cache.SetStringAsync(
                _cacheKeyBuilder.BuildIssuedTokenKey("design", tokenHash),
                JsonSerializer.Serialize(entry, JsonOptions()),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(expiresIn) },
                cancellationToken);

            node.InboundLastTokenIssuedAtUtc = now;
            node.InboundLastFailureReason = string.Empty;
            await _repository.Update(node, cancellationToken);

            return new ConnectionTokenResponse
            {
                AccessToken = token,
                ExpiresIn = expiresIn,
                ExpiresAtUtc = expiresAtUtc,
                Scope = request.Scope,
                KeyId = node.InboundKeyId
            };
        }
        catch (Exception ex)
        {
            node.InboundLastTokenFailedAtUtc = now;
            node.InboundLastFailureReason = ex.Message;
            await _repository.Update(node, cancellationToken);
            throw;
        }
    }

    private void EnsureNodeCanIssueToken(RuntimeNode node)
    {
        if (node.IsDeleted || node.Status != RuntimeNodeStatus.Enabled)
        {
            throw new InvalidOperationException("Runtime node connection is disabled.");
        }

        if (node.InboundCredentialStatus != ConnectionCredentialStatus.Active)
        {
            throw new InvalidOperationException("Inbound credential is not active.");
        }
    }

    private void EnsureRequestedScopesAreAllowed(string requestedValue, string allowedValue)
    {
        var requested = _scopeFormatter.ParseMany(requestedValue);
        var allowed = _scopeFormatter.ParseMany(allowedValue);

        if (requested.Count == 0)
        {
            throw new InvalidOperationException("At least one scope is required.");
        }

        if (requested.Any(scope => !allowed.Contains(scope)))
        {
            throw new InvalidOperationException("Requested scope is not allowed for this node.");
        }
    }

    private static JsonSerializerOptions JsonOptions()
        => new(JsonSerializerDefaults.Web);
}
