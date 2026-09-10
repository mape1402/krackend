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
    /// Gets or sets when the schema was resolved.
    /// </summary>
    public DateTimeOffset ResolvedAtUtc { get; set; }
}
