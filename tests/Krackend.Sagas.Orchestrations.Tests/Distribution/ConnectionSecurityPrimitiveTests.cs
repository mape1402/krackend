using System.Text;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using System.Text.Json;

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Pbkdf2_secret_hash_rejects_empty_secrets(string secret)
    {
        var hasher = new Pbkdf2ConnectionSecretHasher();

        Assert.Throws<ArgumentException>(() => hasher.HashSecret(secret));
    }

    [Theory]
    [InlineData(null, "hash")]
    [InlineData("secret", null)]
    [InlineData("", "hash")]
    [InlineData("secret", "")]
    [InlineData("secret", "unsupported.1000.salt.hash")]
    [InlineData("secret", "pbkdf2-sha256.not-number.salt.hash")]
    public void Pbkdf2_secret_verify_returns_false_for_missing_or_invalid_hash_parts(
        string secret,
        string storedHash)
    {
        var hasher = new Pbkdf2ConnectionSecretHasher();

        Assert.False(hasher.VerifySecret(secret, storedHash));
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

    [Fact]
    public void Scope_formatter_ignores_null_and_empty_inputs_and_rejects_unknown_values()
    {
        var formatter = new DefaultConnectionScopeFormatter();

        Assert.Empty(formatter.FormatMany(null));
        Assert.Empty(formatter.ParseMany(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            formatter.Format((ArtifactDeliveryScope)999));
        Assert.Throws<InvalidOperationException>(() =>
            formatter.ParseMany("artifact:push unsupported:scope"));
    }

    [Fact]
    public void Credential_package_serializer_rejects_empty_and_null_base64_payloads()
    {
        var serializer = new ConnectionCredentialPackageSerializer();
        var nullJson = Convert.ToBase64String(Encoding.UTF8.GetBytes("null"));

        Assert.Throws<InvalidOperationException>(() => serializer.Parse(" "));
        Assert.Throws<InvalidOperationException>(() => serializer.Parse(nullJson));
    }

    [Fact]
    public void Token_hash_rejects_empty_tokens_and_produces_url_safe_hashes()
    {
        var service = new Sha256TokenHashService();

        var hash = service.HashToken("token");

        Assert.NotEmpty(hash);
        Assert.DoesNotContain("+", hash, StringComparison.Ordinal);
        Assert.DoesNotContain("/", hash, StringComparison.Ordinal);
        Assert.DoesNotContain("=", hash, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => service.HashToken(" "));
    }

    [Fact]
    public void Artifact_configuration_contracts_expose_expected_default_shapes()
    {
        var transformation = new DslTransformationConfigurationArtifact { Dsl = "map {}", SourceContextHash = "source", TargetSchemaHash = "target" };
        var validation = new DslValidationConfigurationArtifact { Dsl = "validate {}", SchemaHash = "schema" };
        var humanApproval = new HumanApprovalTaskConfigurationArtifact();
        var schemaSnapshot = new SchemaContractSnapshotArtifact { ContractKind = SchemaContractKind.CommandRequest, ContractKey = "sales.reserve" };

        Assert.Equal(EngineType.DSL, transformation.Engine);
        Assert.Equal("source", transformation.SourceContextHash);
        Assert.Equal("target", transformation.TargetSchemaHash);
        Assert.Equal("{}", transformation.SemanticDiagnosticsJson);
        Assert.Equal(EngineType.DSL, validation.Engine);
        Assert.Equal("schema", validation.SchemaHash);
        Assert.Equal(TaskKind.HumanApproval, humanApproval.Kind);
        Assert.Equal(SchemaContractKind.CommandRequest, schemaSnapshot.ContractKind);
        Assert.Equal("ButterMorph", schemaSnapshot.SchemaFormat);
        Assert.Equal("{}", schemaSnapshot.SchemaJson);
        Assert.NotEqual(default, schemaSnapshot.ResolvedAtUtc);
    }

    [Fact]
    public void Primitive_values_cover_comparison_parsing_and_string_conversion()
    {
        var id = Id.New();
        var sameId = new Id(id.Value);
        var checksum = new Checksum("sha256:abc");
        var reference = new Reference("contract-key");
        var version = new SemanticVersion(1, 2, 3);

        Assert.Equal(0, id.CompareTo(sameId));
        Assert.Equal(1, id.CompareTo(null));
        Assert.Throws<ArgumentException>(() => id.CompareTo("not-an-id"));
        Assert.Equal(id.Value.ToString(), id.ToString());
        Assert.Equal("sha256:abc", checksum.ToString());
        Assert.Throws<ArgumentException>(() => new Checksum(string.Empty));
        Assert.Equal("contract-key", reference.ToString());
        Assert.Equal("1.2.3", version.ToString());
        Assert.True(version.CompareTo(new SemanticVersion(1, 2, 2)) > 0);
        Assert.True(version.CompareTo(new SemanticVersion(1, 3, 0)) < 0);
        Assert.Same(RuntimeArtifactPageCursor.First, RuntimeArtifactPageCursor.First);
        Assert.Equal(0, RuntimeArtifactPageCursor.First.Offset);
    }

    [Fact]
    public void Semantic_version_json_converter_round_trips_and_rejects_invalid_versions()
    {
        var json = JsonSerializer.Serialize(new SemanticVersion(2, 1, 0));
        var parsed = JsonSerializer.Deserialize<SemanticVersion>(json);

        Assert.Equal("\"2.1.0\"", json);
        Assert.Equal(0, new SemanticVersion(2, 1, 0).CompareTo(parsed));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SemanticVersion>("\"\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SemanticVersion>("\"1.0\""));
    }
}
