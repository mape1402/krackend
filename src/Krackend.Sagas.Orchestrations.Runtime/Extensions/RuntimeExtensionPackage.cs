namespace Krackend.Sagas.Orchestrations.Runtime.Extensions;

using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an extension package installed or expected by a runtime node.
/// </summary>
public sealed class RuntimeExtensionPackage
{
    /// <summary>
    /// Gets or sets package id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets source bundle id.
    /// </summary>
    public string BundleId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets extension key.
    /// </summary>
    public string ExtensionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets extension version.
    /// </summary>
    public SemanticVersion Version { get; set; }

    /// <summary>
    /// Gets or sets package checksum.
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets package size.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets manifest snapshot.
    /// </summary>
    public KrackendExtensionManifest Manifest { get; set; }

    /// <summary>
    /// Gets or sets package status.
    /// </summary>
    public RuntimeExtensionPackageStatus Status { get; set; }

    /// <summary>
    /// Gets or sets creation time.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets update time.
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets activation time.
    /// </summary>
    public DateTime? ActivatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets rejection or failure reason.
    /// </summary>
    public string StatusReason { get; set; } = string.Empty;
}

