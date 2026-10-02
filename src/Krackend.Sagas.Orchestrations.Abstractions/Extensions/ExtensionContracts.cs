namespace Krackend.Sagas.Orchestrations.Abstractions.Extensions;

using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Describes how an extension may be loaded by a host.
/// </summary>
public enum ExtensionLoadMode
{
    /// <summary>
    /// The extension is part of Krackend itself.
    /// </summary>
    BuiltIn = 0,

    /// <summary>
    /// The host application registered the extension explicitly.
    /// </summary>
    HostRegistered = 1,

    /// <summary>
    /// The extension is distributed as an external assembly bundle.
    /// </summary>
    ExternalAssembly = 2,

    /// <summary>
    /// The extension is executed by a remote service.
    /// </summary>
    RemoteService = 3,

    /// <summary>
    /// The extension is known but not loadable.
    /// </summary>
    Disabled = 4
}

/// <summary>
/// Describes the trust boundary for an extension.
/// </summary>
public enum ExtensionTrustLevel
{
    /// <summary>
    /// Code shipped as part of Krackend.
    /// </summary>
    BuiltIn = 0,

    /// <summary>
    /// Code explicitly trusted by the host application.
    /// </summary>
    HostTrusted = 1,

    /// <summary>
    /// Code from a trusted publisher that still requires policy validation.
    /// </summary>
    PublisherTrusted = 2,

    /// <summary>
    /// Untrusted extension code. This should require external isolation before activation.
    /// </summary>
    Untrusted = 3
}

/// <summary>
/// Describes one capability exposed by an extension bundle.
/// </summary>
public sealed record ExtensionCapabilityDescriptor(
    CapabilityKey Key,
    SemanticVersion Version,
    string Kind,
    string Name,
    string Description = "")
{
    /// <summary>
    /// Gets the runtime requirements declared by the capability.
    /// </summary>
    public ExecutionRuntimeRequirementsArtifact RuntimeRequirements { get; init; } =
        ExecutionRuntimeRequirementsArtifact.Empty;
}

/// <summary>
/// Defines a Krackend extension manifest.
/// </summary>
public sealed record KrackendExtensionManifest(
    ExtensionKey Key,
    SemanticVersion Version,
    string Name,
    string Publisher,
    ExtensionLoadMode LoadMode,
    ExtensionTrustLevel TrustLevel)
{
    /// <summary>
    /// Gets the manifest schema version.
    /// </summary>
    public int ManifestVersion { get; init; } = 1;

    /// <summary>
    /// Gets the capabilities exposed by this extension.
    /// </summary>
    public IReadOnlyList<ExtensionCapabilityDescriptor> Capabilities { get; init; } =
        Array.Empty<ExtensionCapabilityDescriptor>();

    /// <summary>
    /// Gets the files included by the bundle.
    /// </summary>
    public IReadOnlyList<ExtensionBundleFileDescriptor> Files { get; init; } =
        Array.Empty<ExtensionBundleFileDescriptor>();
}

/// <summary>
/// Describes a file inside an extension bundle.
/// </summary>
public sealed record ExtensionBundleFileDescriptor(
    string Path,
    string Sha256,
    long SizeBytes,
    string Purpose);

/// <summary>
/// References a capability required by an orchestration artifact.
/// </summary>
public sealed record RequiredCapabilityArtifact(
    string ExtensionKey,
    string CapabilityKey,
    SemanticVersion Version,
    string Kind = "");

/// <summary>
/// References a bundle required by an orchestration artifact.
/// </summary>
public sealed record RequiredExtensionBundleArtifact(
    string BundleId,
    string ExtensionKey,
    SemanticVersion Version,
    string Sha256 = "");

