using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class ControlPlaneConnectionTokenTests
{
    [Fact]
    public async Task Design_issues_and_validates_runtime_tokens_by_scope_and_node()
    {
        var hasher = new Pbkdf2ConnectionSecretHasher();
        var repository = new FakeRuntimeNodeRepository();
        var node = new RuntimeNode
        {
            Id = Id.New(),
            Code = "local-runtime",
            Name = "Local Runtime",
            IsEnabled = true,
            Status = RuntimeNodeStatus.Enabled,
            AccessTokenTtlSeconds = 120,
            TokenValidationCacheTtlSeconds = 30,
            InboundClientId = "runtime-client",
            InboundKeyId = "runtime-key",
            InboundSecretHash = hasher.HashSecret("runtime-secret"),
            InboundAllowedScopes = "release:read artifact:read artifact:ack connection:validate",
            InboundCredentialStatus = ConnectionCredentialStatus.Active
        };
        await repository.Create(node);

        var cache = CreateCache();
        var tokenHashService = new Sha256TokenHashService();
        var scopeFormatter = new DefaultConnectionScopeFormatter();
        var cacheKeyBuilder = new ConnectionTokenCacheKeyBuilder();
        var issuer = new ControlPlaneConnectionTokenIssuer(
            repository,
            hasher,
            new SecureConnectionSecretGenerator(),
            tokenHashService,
            scopeFormatter,
            cacheKeyBuilder,
            cache);
        var validator = new ControlPlaneConnectionTokenValidator(
            repository,
            tokenHashService,
            scopeFormatter,
            cacheKeyBuilder,
            cache);

        var token = await issuer.IssueAsync(new ConnectionTokenRequest
        {
            ClientId = "runtime-client",
            ClientSecret = "runtime-secret",
            Scope = "release:read artifact:read"
        });

        var valid = await validator.ValidateAsync(
            token.AccessToken,
            node.Id.ToString(),
            [ArtifactDeliveryScope.ReleaseRead]);
        var wrongNode = await validator.ValidateAsync(
            token.AccessToken,
            Id.New().ToString(),
            [ArtifactDeliveryScope.ReleaseRead]);

        Assert.True(valid.Succeeded);
        Assert.Equal(node.Id.ToString(), valid.Principal.NodeId);
        Assert.False(wrongNode.Succeeded);
    }

    [Theory]
    [InlineData("password", "Only client_credentials grant type is supported.")]
    [InlineData("client_credentials", "Client credential is not registered.")]
    public async Task DesignIssuerRejectsUnsupportedGrantAndUnknownClient(
        string grantType,
        string expectedMessage)
    {
        var issuer = CreateIssuer(new FakeRuntimeNodeRepository(), CreateCache());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            issuer.IssueAsync(new ConnectionTokenRequest
            {
                GrantType = grantType,
                ClientId = "missing-client",
                ClientSecret = "runtime-secret",
                Scope = "artifact:read"
            }));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(RuntimeNodeStatus.Pending, false, ConnectionCredentialStatus.Active, false, "artifact:read", "runtime-secret", "Runtime node connection is disabled.")]
    [InlineData(RuntimeNodeStatus.Enabled, true, ConnectionCredentialStatus.Missing, false, "artifact:read", "runtime-secret", "Inbound credential is not active.")]
    [InlineData(RuntimeNodeStatus.Enabled, true, ConnectionCredentialStatus.Active, false, "", "runtime-secret", "At least one scope is required.")]
    [InlineData(RuntimeNodeStatus.Enabled, true, ConnectionCredentialStatus.Active, false, "artifact:push", "runtime-secret", "Requested scope is not allowed for this node.")]
    [InlineData(RuntimeNodeStatus.Enabled, true, ConnectionCredentialStatus.Active, false, "artifact:read", "wrong-secret", "Client credential secret is invalid.")]
    public async Task DesignIssuerRecordsNodeFailureWhenCredentialCannotIssueToken(
        RuntimeNodeStatus status,
        bool isEnabled,
        ConnectionCredentialStatus credentialStatus,
        bool isDeleted,
        string scope,
        string clientSecret,
        string expectedMessage)
    {
        var hasher = new Pbkdf2ConnectionSecretHasher();
        var repository = new FakeRuntimeNodeRepository();
        var node = CreateNode(hasher);
        node.Status = status;
        node.IsEnabled = isEnabled;
        node.InboundCredentialStatus = credentialStatus;
        node.IsDeleted = isDeleted;
        await repository.Create(node);
        var issuer = CreateIssuer(repository, CreateCache(), hasher);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            issuer.IssueAsync(new ConnectionTokenRequest
            {
                ClientId = "runtime-client",
                ClientSecret = clientSecret,
                Scope = scope
            }));

        var persisted = await repository.GetById(node.Id);
        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(expectedMessage, persisted.InboundLastFailureReason);
        Assert.NotNull(persisted.InboundLastTokenFailedAtUtc);
    }

    [Fact]
    public async Task DesignValidatorHandlesCacheAndCredentialFailureBranches()
    {
        var hasher = new Pbkdf2ConnectionSecretHasher();
        var repository = new FakeRuntimeNodeRepository();
        var node = CreateNode(hasher);
        await repository.Create(node);
        var cache = CreateCache();
        var tokenHashService = new Sha256TokenHashService();
        var scopeFormatter = new DefaultConnectionScopeFormatter();
        var cacheKeyBuilder = new ConnectionTokenCacheKeyBuilder();
        var validator = new ControlPlaneConnectionTokenValidator(
            repository,
            tokenHashService,
            scopeFormatter,
            cacheKeyBuilder,
            cache);

        var missing = await validator.ValidateAsync(" ", node.Id.ToString(), [ArtifactDeliveryScope.ArtifactRead]);
        var unknown = await validator.ValidateAsync("unknown-token", node.Id.ToString(), [ArtifactDeliveryScope.ArtifactRead]);

        Assert.False(missing.Succeeded);
        Assert.False(unknown.Succeeded);

        var expiredToken = "expired-token";
        await cache.SetStringAsync(
            cacheKeyBuilder.BuildIssuedTokenKey("design", tokenHashService.HashToken(expiredToken)),
            JsonSerializer.Serialize(new ConnectionTokenCacheEntry
            {
                NodeId = node.Id.ToString(),
                NodeKey = node.Code,
                ClientId = node.InboundClientId,
                KeyId = node.InboundKeyId,
                Scopes = "artifact:read",
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1)
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var expired = await validator.ValidateAsync(expiredToken, node.Id.ToString(), [ArtifactDeliveryScope.ArtifactRead]);
        Assert.False(expired.Succeeded);

        var wrongNodeToken = "wrong-node-token";
        await cache.SetStringAsync(
            cacheKeyBuilder.BuildIssuedTokenKey("design", tokenHashService.HashToken(wrongNodeToken)),
            JsonSerializer.Serialize(new ConnectionTokenCacheEntry
            {
                NodeId = node.Id.ToString(),
                NodeKey = node.Code,
                ClientId = node.InboundClientId,
                KeyId = node.InboundKeyId,
                Scopes = "artifact:read",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var wrongNode = await validator.ValidateAsync(wrongNodeToken, Id.New().ToString(), [ArtifactDeliveryScope.ArtifactRead]);
        Assert.False(wrongNode.Succeeded);

        var rotatedToken = "rotated-token";
        await cache.SetStringAsync(
            cacheKeyBuilder.BuildIssuedTokenKey("design", tokenHashService.HashToken(rotatedToken)),
            JsonSerializer.Serialize(new ConnectionTokenCacheEntry
            {
                NodeId = node.Id.ToString(),
                NodeKey = node.Code,
                ClientId = "old-client",
                KeyId = node.InboundKeyId,
                Scopes = "artifact:read",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var rotated = await validator.ValidateAsync(rotatedToken, node.Id.ToString(), [ArtifactDeliveryScope.ArtifactRead]);
        Assert.False(rotated.Succeeded);

        var disabledToken = "disabled-token";
        await cache.SetStringAsync(
            cacheKeyBuilder.BuildIssuedTokenKey("design", tokenHashService.HashToken(disabledToken)),
            JsonSerializer.Serialize(new ConnectionTokenCacheEntry
            {
                NodeId = node.Id.ToString(),
                NodeKey = node.Code,
                ClientId = node.InboundClientId,
                KeyId = node.InboundKeyId,
                Scopes = "artifact:read",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        node.InboundCredentialStatus = ConnectionCredentialStatus.Missing;
        await repository.Update(node);
        var inactive = await validator.ValidateAsync(disabledToken, node.Id.ToString(), [ArtifactDeliveryScope.ArtifactRead]);
        Assert.False(inactive.Succeeded);
    }

    [Fact]
    public async Task DesignValidatorUsesCachedPrincipalWhenNodeAndScopesMatch()
    {
        var repository = new FakeRuntimeNodeRepository();
        var cache = CreateCache();
        var tokenHashService = new Sha256TokenHashService();
        var cacheKeyBuilder = new ConnectionTokenCacheKeyBuilder();
        var token = "cached-token";
        var nodeId = Id.New().ToString();
        var principal = new ConnectionTokenPrincipal
        {
            NodeId = nodeId,
            NodeKey = "runtime",
            ClientId = "client",
            KeyId = "key",
            Scopes = [ArtifactDeliveryScope.ArtifactRead, ArtifactDeliveryScope.ReleaseRead]
        };
        await cache.SetStringAsync(
            cacheKeyBuilder.BuildValidationKey("design", tokenHashService.HashToken(token)),
            JsonSerializer.Serialize(principal, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var validator = new ControlPlaneConnectionTokenValidator(
            repository,
            tokenHashService,
            new DefaultConnectionScopeFormatter(),
            cacheKeyBuilder,
            cache);

        var result = await validator.ValidateAsync(token, nodeId, []);

        Assert.True(result.Succeeded);
        Assert.Equal(nodeId, result.Principal.NodeId);
    }

    [Fact]
    public void DesignRuntimeNodeSecretProtectorRoundTripsSecretMaterial()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"krackend-control-dp-{Guid.NewGuid():N}"));
        var provider = DataProtectionProvider.Create(directory);
        var protector = new DataProtectionControlPlaneRuntimeNodeSecretProtector(provider);

        var protectedValue = protector.Protect("runtime-secret");

        Assert.NotEqual("runtime-secret", protectedValue);
        Assert.Equal("runtime-secret", protector.Unprotect(protectedValue));
        Assert.NotEmpty(protector.Protect(null!));
    }

    private static ControlPlaneConnectionTokenIssuer CreateIssuer(
        FakeRuntimeNodeRepository repository,
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

    private static RuntimeNode CreateNode(IConnectionSecretHasher hasher)
        => new()
        {
            Id = Id.New(),
            Code = "local-runtime",
            Name = "Local Runtime",
            IsEnabled = true,
            Status = RuntimeNodeStatus.Enabled,
            AccessTokenTtlSeconds = 120,
            TokenValidationCacheTtlSeconds = 30,
            InboundClientId = "runtime-client",
            InboundKeyId = "runtime-key",
            InboundSecretHash = hasher.HashSecret("runtime-secret"),
            InboundAllowedScopes = "release:read artifact:read artifact:ack connection:validate",
            InboundCredentialStatus = ConnectionCredentialStatus.Active
        };

    private static MemoryDistributedCache CreateCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));
}
