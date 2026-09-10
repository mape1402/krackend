namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Represents a schema snapshot resolved for a design-time schema binding.
/// </summary>
public sealed class SchemaContractSnapshot
{
    /// <summary>
    /// Gets or sets the schema contract kind.
    /// </summary>
    public SchemaContractKind ContractKind { get; set; } = SchemaContractKind.Unspecified;

    /// <summary>
    /// Gets or sets the schema format.
    /// </summary>
    public string SchemaFormat { get; set; } = "ButterMorph";

    /// <summary>
    /// Gets or sets the schema JSON document.
    /// </summary>
    public string SchemaJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the schema content hash.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the schema was resolved.
    /// </summary>
    public DateTimeOffset ResolvedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
