using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class ConnectionSecurityPrimitiveTests
{
    [Fact]
    public void Pbkdf2_secret_hash_verifies_without_storing_the_plain_secret()
    {
        var hasher = new Pbkdf2ConnectionSecretHasher();

        var hash = hasher.HashSecret("super-secret");

        Assert.DoesNotContain("super-secret", hash, StringComparison.Ordinal);
        Assert.True(hasher.VerifySecret("super-secret", hash));
        Assert.False(hasher.VerifySecret("wrong-secret", hash));
    }

    [Fact]
    public void Scope_formatter_round_trips_artifact_delivery_scopes()
    {
        var formatter = new DefaultConnectionScopeFormatter();

        var formatted = formatter.FormatMany(
            [ArtifactDeliveryScope.ArtifactPush, ArtifactDeliveryScope.ConnectionValidate]);
        var parsed = formatter.ParseMany(formatted);

        Assert.Contains(ArtifactDeliveryScope.ArtifactPush, parsed);
        Assert.Contains(ArtifactDeliveryScope.ConnectionValidate, parsed);
        Assert.Equal(2, parsed.Count);
    }
}
