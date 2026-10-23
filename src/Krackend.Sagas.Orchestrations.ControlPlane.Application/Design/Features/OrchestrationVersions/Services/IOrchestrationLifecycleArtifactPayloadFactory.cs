using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Builds compact lifecycle-action artifact payloads for already deployed orchestration versions.
/// </summary>
public interface IOrchestrationLifecycleArtifactPayloadFactory
{
    /// <summary>
    /// Creates a deprecation or archive artifact payload and its immutable payload checksum.
    /// </summary>
    /// <param name="definition">The owning orchestration definition.</param>
    /// <param name="version">The immutable version targeted by the lifecycle action.</param>
    /// <param name="operation">The lifecycle operation to apply.</param>
    /// <returns>The serialized action payload and checksum used to protect it in transit.</returns>
    OrchestrationLifecycleArtifactPayload CreatePayload(
        OrchestrationDefinition definition,
        OrchestrationVersion version,
        OrchestrationVersionReviewOperation operation);
}

/// <summary>
/// Contains the serialized lifecycle payload and its transport integrity checksum.
/// </summary>
public sealed record OrchestrationLifecycleArtifactPayload(string Json, Checksum Checksum);
