using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

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

    private static MemoryDistributedCache CreateCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));
}
