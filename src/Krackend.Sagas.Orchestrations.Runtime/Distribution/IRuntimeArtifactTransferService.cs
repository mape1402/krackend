using Krackend.Sagas.Orchestrations.Abstractions.Distribution;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Exports a runtime artifact as a portable release candidate and imports it through
/// the normal runtime deployment pipeline.
/// </summary>
public interface IRuntimeArtifactTransferService
{
    /// <summary>Creates an integrity-protected portable package for a runtime artifact.</summary>
    Task<RuntimeArtifactTransferPackage> ExportAsync(string artifactId, CancellationToken cancellationToken = default);

    /// <summary>Serializes a portable package for download.</summary>
    string Serialize(RuntimeArtifactTransferPackage package);

    /// <summary>Validates and describes a portable package without applying it.</summary>
    RuntimeArtifactTransferInspection Inspect(string packageJson);

    /// <summary>Validates and applies a portable package through the standard deployment service.</summary>
    Task<RuntimeArtifactDeploymentResult> ImportAsync(
        string packageJson,
        string importedBy,
        CancellationToken cancellationToken = default);
}
