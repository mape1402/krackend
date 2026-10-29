using Krackend.Sagas.Orchestrations.Abstractions.Distribution;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Integrity-protected runtime artifact package used to move a release candidate
/// between runtime installations without bypassing deployment safeguards.
/// </summary>
public sealed class RuntimeArtifactTransferPackage
{
    /// <summary>Current portable package schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Stable discriminator for runtime artifact transfer packages.</summary>
    public const string Kind = "krackend.runtime-artifact.release";

    /// <summary>Gets or sets the portable package schema version.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Gets or sets the portable package discriminator.</summary>
    public string PackageKind { get; set; } = Kind;

    /// <summary>Gets or sets when the package was exported in UTC.</summary>
    public DateTime ExportedOnUtc { get; set; }

    /// <summary>Gets or sets the source runtime artifact id.</summary>
    public string SourceArtifactId { get; set; } = string.Empty;

    /// <summary>Gets or sets the deployable release candidate.</summary>
    public RuntimeArtifactDeliveryPackage Delivery { get; set; } = new();

    /// <summary>Gets or sets the SHA-256 checksum of the package manifest.</summary>
    public string PackageChecksum { get; set; } = string.Empty;
}

/// <summary>Read-only details returned when a portable runtime package is inspected.</summary>
public sealed record RuntimeArtifactTransferInspection(
    int SchemaVersion,
    string PackageKind,
    string SourceArtifactId,
    string OrchestrationKey,
    string Version,
    string ArtifactType,
    string ReleaseStrategy,
    DateTime ExportedOnUtc);
