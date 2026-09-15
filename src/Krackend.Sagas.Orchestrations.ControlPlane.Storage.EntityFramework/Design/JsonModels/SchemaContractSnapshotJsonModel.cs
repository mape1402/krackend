using Krackend.Sagas.Orchestrations.SchemaRegistry;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents a persisted schema contract snapshot.
/// </summary>
public sealed class SchemaContractSnapshotJsonModel
{
    /// <summary>
    /// Gets or sets the contract kind.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SchemaContractKind ContractKind { get; set; }

    /// <summary>
    /// Gets or sets the configured schema registry provider id.
    /// </summary>
    public string RegistryProviderId { get; set; }

    /// <summary>
    /// Gets or sets the configured schema registry provider key.
    /// </summary>
    public string RegistryProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the external contract identifier.
    /// </summary>
    public string ContractId { get; set; }

    /// <summary>
    /// Gets or sets the logical contract key.
    /// </summary>
    public string ContractKey { get; set; }

    /// <summary>
    /// Gets or sets the semantic or provider-native contract version.
    /// </summary>
    public string ContractVersion { get; set; }

    /// <summary>
    /// Gets or sets the schema format.
    /// </summary>
    public string SchemaFormat { get; set; }

    /// <summary>
    /// Gets or sets the schema JSON document.
    /// </summary>
    public string SchemaJson { get; set; }

    /// <summary>
    /// Gets or sets the schema content hash.
    /// </summary>
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific source artifact id.
    /// </summary>
    public string SourceArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the provider that resolved the snapshot.
    /// </summary>
    public string ResolvedBy { get; set; }

    /// <summary>
    /// Gets or sets when the schema was resolved.
    /// </summary>
    public DateTimeOffset ResolvedAtUtc { get; set; }
}
