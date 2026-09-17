namespace Krackend.Security.Core;

/// <summary>
/// Defines stable assignment source names.
/// </summary>
public static class KrackendAssignmentSources
{
    /// <summary>
    /// Assignment created manually by a product administrator.
    /// </summary>
    public const string Manual = "Manual";

    /// <summary>
    /// Assignment created from bootstrap configuration.
    /// </summary>
    public const string BootstrapConfig = "BootstrapConfig";

    /// <summary>
    /// Assignment resolved from an external group mapping.
    /// </summary>
    public const string ExternalGroup = "ExternalGroup";
}
