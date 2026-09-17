using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class RuntimeConnectionTokenTests
{
    [Fact]
    public async Task Runtime_issues_and_validates_design_tokens_by_scope()
    {
        var hasher = new Pbkdf2ConnectionSecretHasher();
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = new RuntimeDesignNode
        {
            Id = Id.New(),
            Key = "local-design",
            Name = "Local Design",
            Status = RuntimeDesignNodeStatus.Enabled,
            IsEnabled = true,
            AccessTokenTtlSeconds = 120,
            TokenValidationCacheTtlSeconds = 30,
            InboundClientId = "design-client",
            InboundKeyId = "design-key",
            InboundSecretHash = hasher.HashSecret("design-secret"),
            InboundAllowedScopes = "artifact:push connection:validate",
            InboundCredentialStatus = ConnectionCredentialStatus.Active
        };
        await repository.UpsertAsync(node);

        var cache = CreateCache();
        var tokenHashService = new Sha256TokenHashService();
        var scopeFormatter = new DefaultConnectionScopeFormatter();
        var cacheKeyBuilder = new ConnectionTokenCacheKeyBuilder();
        var issuer = new RuntimeConnectionTokenIssuer(
            repository,
            hasher,
            new SecureConnectionSecretGenerator(),
            tokenHashService,
            scopeFormatter,
            cacheKeyBuilder,
            cache);
        var validator = new RuntimeConnectionTokenValidator(
            repository,
            tokenHashService,
            scopeFormatter,
            cacheKeyBuilder,
            cache);

        var token = await issuer.IssueAsync(new ConnectionTokenRequest
        {
            ClientId = "design-client",
            ClientSecret = "design-secret",
            Scope = "artifact:push"
        });

        var valid = await validator.ValidateAsync(token.AccessToken, [ArtifactDeliveryScope.ArtifactPush]);
        var forbidden = await validator.ValidateAsync(token.AccessToken, [ArtifactDeliveryScope.ArtifactRead]);

        Assert.True(valid.Succeeded);
        Assert.Equal(node.Id.ToString(), valid.Principal.NodeId);
        Assert.False(forbidden.Succeeded);
    }

    [Theory]
    [InlineData("password", "Only client_credentials grant type is supported.")]
    [InlineData("client_credentials", "Client credential is not registered.")]
    public async Task RuntimeIssuerRejectsUnsupportedGrantAndUnknownClient(
        string grantType,
        string expectedMessage)
    {
        var issuer = CreateIssuer(new FakeRuntimeDesignNodeRepository(), CreateCache());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            issuer.IssueAsync(new ConnectionTokenRequest
            {
                GrantType = grantType,
                ClientId = "missing-client",
                ClientSecret = "design-secret",
                Scope = "artifact:push"
            }));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(RuntimeDesignNodeStatus.Pending, true, ConnectionCredentialStatus.Active, "artifact:push", "design-secret", "Design node connection is disabled.")]
    [InlineData(RuntimeDesignNodeStatus.Enabled, false, ConnectionCredentialStatus.Active, "artifact:push", "design-secret", "Design node connection is disabled.")]
    [InlineData(RuntimeDesignNodeStatus.Enabled, true, ConnectionCredentialStatus.Missing, "artifact:push", "design-secret", "Inbound credential is not active.")]
    [InlineData(RuntimeDesignNodeStatus.Enabled, true, ConnectionCredentialStatus.Active, "", "design-secret", "At least one scope is required.")]
    [InlineData(RuntimeDesignNodeStatus.Enabled, true, ConnectionCredentialStatus.Active, "artifact:read", "design-secret", "Requested scope is not allowed for this node.")]
    [InlineData(RuntimeDesignNodeStatus.Enabled, true, ConnectionCredentialStatus.Active, "artifact:push", "wrong-secret", "Client credential secret is invalid.")]
    public async Task RuntimeIssuerRecordsNodeFailureWhenCredentialCannotIssueToken(
        RuntimeDesignNodeStatus status,
        bool isEnabled,
        ConnectionCredentialStatus credentialStatus,
        string scope,
        string clientSecret,
        string expectedMessage)
    {
        var hasher = new Pbkdf2ConnectionSecretHasher();
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = CreateNode(hasher);
        node.Status = status;
        node.IsEnabled = isEnabled;
        node.InboundCredentialStatus = credentialStatus;
        await repository.UpsertAsync(node);
        var issuer = CreateIssuer(repository, CreateCache(), hasher);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            issuer.IssueAsync(new ConnectionTokenRequest
            {
                ClientId = "design-client",
                ClientSecret = clientSecret,
                Scope = scope
            }));

        var persisted = await repository.GetByIdAsync(node.Id);
        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(expectedMessage, persisted.InboundLastFailureReason);
        Assert.NotNull(persisted.InboundLastTokenFailedAtUtc);
    }

    [Fact]
    public async Task RuntimeValidatorHandlesCacheAndCredentialFailureBranches()
    {
        var hasher = new Pbkdf2ConnectionSecretHasher();
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = CreateNode(hasher);
        await repository.UpsertAsync(node);
        var cache = CreateCache();
        var tokenHashService = new Sha256TokenHashService();
        var scopeFormatter = new DefaultConnectionScopeFormatter();
        var cacheKeyBuilder = new ConnectionTokenCacheKeyBuilder();
        var validator = new RuntimeConnectionTokenValidator(
            repository,
            tokenHashService,
            scopeFormatter,
            cacheKeyBuilder,
            cache);

        var missing = await validator.ValidateAsync(" ", [ArtifactDeliveryScope.ArtifactPush]);
        var unknown = await validator.ValidateAsync("unknown-token", [ArtifactDeliveryScope.ArtifactPush]);

        Assert.False(missing.Succeeded);
        Assert.False(unknown.Succeeded);

        var expiredToken = "expired-token";
        await cache.SetStringAsync(
            cacheKeyBuilder.BuildIssuedTokenKey("runtime", tokenHashService.HashToken(expiredToken)),
            JsonSerializer.Serialize(new ConnectionTokenCacheEntry
            {
                NodeId = node.Id.ToString(),
                NodeKey = node.Key,
                ClientId = node.InboundClientId,
                KeyId = node.InboundKeyId,
                Scopes = "artifact:push",
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1)
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var expired = await validator.ValidateAsync(expiredToken, [ArtifactDeliveryScope.ArtifactPush]);
        Assert.False(expired.Succeeded);

        var rotatedToken = "rotated-token";
        await cache.SetStringAsync(
            cacheKeyBuilder.BuildIssuedTokenKey("runtime", tokenHashService.HashToken(rotatedToken)),
            JsonSerializer.Serialize(new ConnectionTokenCacheEntry
            {
                NodeId = node.Id.ToString(),
                NodeKey = node.Key,
                ClientId = "old-client",
                KeyId = node.InboundKeyId,
                Scopes = "artifact:push",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var rotated = await validator.ValidateAsync(rotatedToken, [ArtifactDeliveryScope.ArtifactPush]);
        Assert.False(rotated.Succeeded);

        var disabledToken = "disabled-token";
        await cache.SetStringAsync(
            cacheKeyBuilder.BuildIssuedTokenKey("runtime", tokenHashService.HashToken(disabledToken)),
            JsonSerializer.Serialize(new ConnectionTokenCacheEntry
            {
                NodeId = node.Id.ToString(),
                NodeKey = node.Key,
                ClientId = node.InboundClientId,
                KeyId = node.InboundKeyId,
                Scopes = "artifact:push",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        node.InboundCredentialStatus = ConnectionCredentialStatus.Missing;
        await repository.UpsertAsync(node);
        var inactive = await validator.ValidateAsync(disabledToken, [ArtifactDeliveryScope.ArtifactPush]);
        Assert.False(inactive.Succeeded);
    }

    [Fact]
    public async Task RuntimeValidatorUsesCachedPrincipalWhenScopesMatch()
    {
        var repository = new FakeRuntimeDesignNodeRepository();
        var cache = CreateCache();
        var tokenHashService = new Sha256TokenHashService();
        var cacheKeyBuilder = new ConnectionTokenCacheKeyBuilder();
        var token = "cached-token";
        var principal = new ConnectionTokenPrincipal
        {
            NodeId = Id.New().ToString(),
            NodeKey = "design",
            ClientId = "client",
            KeyId = "key",
            Scopes = [ArtifactDeliveryScope.ArtifactPush, ArtifactDeliveryScope.ConnectionValidate]
        };
        await cache.SetStringAsync(
            cacheKeyBuilder.BuildValidationKey("runtime", tokenHashService.HashToken(token)),
            JsonSerializer.Serialize(principal, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var validator = new RuntimeConnectionTokenValidator(
            repository,
            tokenHashService,
            new DefaultConnectionScopeFormatter(),
            cacheKeyBuilder,
            cache);

        var result = await validator.ValidateAsync(token, []);

        Assert.True(result.Succeeded);
        Assert.Equal(principal.NodeId, result.Principal.NodeId);
    }

    private static RuntimeConnectionTokenIssuer CreateIssuer(
        FakeRuntimeDesignNodeRepository repository,
        IDistributedCache cache,
        IConnectionSecretHasher? hasher = null)
        => new(
            repository,
            hasher ?? new Pbkdf2ConnectionSecretHasher(),
            new SecureConnectionSecretGenerator(),
            new Sha256TokenHashService(),
            new DefaultConnectionScopeFormatter(),
            new ConnectionTokenCacheKeyBuilder(),
            cache);

    private static RuntimeDesignNode CreateNode(IConnectionSecretHasher hasher)
        => new()
        {
            Id = Id.New(),
            Key = "local-design",
            Name = "Local Design",
            Status = RuntimeDesignNodeStatus.Enabled,
            IsEnabled = true,
            AccessTokenTtlSeconds = 120,
            TokenValidationCacheTtlSeconds = 30,
            InboundClientId = "design-client",
            InboundKeyId = "design-key",
            InboundSecretHash = hasher.HashSecret("design-secret"),
            InboundAllowedScopes = "artifact:push connection:validate",
            InboundCredentialStatus = ConnectionCredentialStatus.Active
        };

    private static MemoryDistributedCache CreateCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));
}
