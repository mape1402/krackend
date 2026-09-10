using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents SchemaBindingJsonModel.
/// </summary>
public sealed class SchemaBindingJsonModel
{
    /// <summary>
    /// Gets or sets Id.
    /// </summary>
    public string Id { get; set; }
    /// <summary>
    /// Gets or sets ElementType.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ElementType ElementType { get; set; }
    /// <summary>
    /// Gets or sets ElementId.
    /// </summary>
    public string ElementId { get; set; }
    /// <summary>
    /// Gets or sets ContractId.
    /// </summary>
    public string ContractId { get; set; }
    /// <summary>
    /// Gets or sets ContractKey.
    /// </summary>
    public string ContractKey { get; set; }
    /// <summary>
    /// Gets or sets ContractVersion.
    /// </summary>
    public string ContractVersion { get; set; }
    /// <summary>
    /// Gets or sets RegistryProviderId.
    /// </summary>
    public string RegistryProviderId { get; set; }
    /// <summary>
    /// Gets or sets registry provider key.
    /// </summary>
    public string RegistryProviderKey { get; set; }
    /// <summary>
    /// Gets or sets contract kind.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SchemaContractKind ContractKind { get; set; }
    /// <summary>
    /// Gets or sets StrictMode.
    /// </summary>
    public bool StrictMode { get; set; }
    /// <summary>
    /// Gets or sets whether schema validation is enabled.
    /// </summary>
    public bool IsValidationEnabled { get; set; }
    /// <summary>
    /// Gets or sets the resolved schema snapshot.
    /// </summary>
    public SchemaContractSnapshotJsonModel Snapshot { get; set; }
}
