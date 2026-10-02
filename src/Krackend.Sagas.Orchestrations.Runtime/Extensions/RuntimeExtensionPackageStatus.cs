namespace Krackend.Sagas.Orchestrations.Runtime.Extensions;

/// <summary>
/// Runtime lifecycle state for an extension package deployed to a runtime node.
/// </summary>
public enum RuntimeExtensionPackageStatus
{
    /// <summary>
    /// The package is expected but has not been downloaded.
    /// </summary>
    PendingDownload = 0,

    /// <summary>
    /// The package bytes have been downloaded.
    /// </summary>
    Downloaded = 1,

    /// <summary>
    /// The package has passed runtime validation.
    /// </summary>
    Validated = 2,

    /// <summary>
    /// The package has been installed on the runtime node.
    /// </summary>
    Installed = 3,

    /// <summary>
    /// The package is active and can satisfy orchestration capability requirements.
    /// </summary>
    Activated = 4,

    /// <summary>
    /// The package is installed but disabled by policy.
    /// </summary>
    Disabled = 5,

    /// <summary>
    /// The package was rejected by validation or policy.
    /// </summary>
    Rejected = 6,

    /// <summary>
    /// The package failed during deployment or activation.
    /// </summary>
    Failed = 7
}

