using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Builds self-contained lifecycle action envelopes without reserializing the executable snapshot.
/// </summary>
public sealed class OrchestrationLifecycleArtifactPayloadFactory : IOrchestrationLifecycleArtifactPayloadFactory
{
    /// <inheritdoc />
    public OrchestrationLifecycleArtifactPayload CreatePayload(
        OrchestrationDefinition definition,
        OrchestrationVersion version,
        OrchestrationVersionReviewOperation operation)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(version);

        if (!version.Version.HasValue)
        {
            throw new InvalidOperationException(
                $"Orchestration version '{version.Id}' must have a semantic version before a lifecycle artifact can be generated.");
        }

        var payload = new OrchestrationVersionLifecycleArtifact
        {
            Operation = operation switch
            {
                OrchestrationVersionReviewOperation.Deprecate => "deprecate",
                OrchestrationVersionReviewOperation.Archive => "archive",
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Only lifecycle review operations are supported."),
            },
            OrchestrationDefinitionId = definition.Id.ToString(),
            OrchestrationDefinitionKey = definition.Key,
            OrchestrationVersionId = version.Id.ToString(),
            Version = version.Version.Value.ToString(),
            TargetArtifactChecksum = version.Checksum.Value,
        };
        var json = JsonSerializer.Serialize(payload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return new OrchestrationLifecycleArtifactPayload(
            json,
            new Checksum($"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}"));
    }
}
