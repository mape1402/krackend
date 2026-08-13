namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a schema contract binding associated with an orchestration element.
/// </summary>
public sealed class SchemaBinding
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets element type.
    /// </summary>
    public required ElementType ElementType { get; set; }

    /// <summary>
    /// Gets or sets element id.
    /// </summary>
    public Id ElementId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the contract.
    /// </summary>
    /// <remarks>The ContractId property is required and must be set to a valid, non-empty string to identify
    /// a specific contract instance. This value should be unique within the context where contracts are
    /// managed.</remarks>
    public required Id ContractId { get; set; }

    /// <summary>
    /// Gets or sets contract key.
    /// </summary>
    public required string ContractKey { get; set; }

    /// <summary>
    /// Gets or sets contract version.
    /// </summary>
    public SemanticVersion ContractVersion { get; set; }

    /// <summary>
    /// Gets or sets registry provider id.
    /// </summary>
    public Id RegistryProviderId { get; set; }

    /// <summary>
    /// Gets or sets strict mode.
    /// </summary>
    public bool StrictMode { get; set; }

    /// <summary>
    /// Gets or sets whether schema validation is enabled.
    /// </summary>
    public bool IsValidationEnabled { get; set; }
}
