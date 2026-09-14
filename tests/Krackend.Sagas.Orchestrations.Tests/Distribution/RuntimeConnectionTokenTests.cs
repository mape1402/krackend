using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

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

    private static MemoryDistributedCache CreateCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));
}
