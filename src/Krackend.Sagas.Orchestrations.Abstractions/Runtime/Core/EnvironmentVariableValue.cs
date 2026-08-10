using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents environment variable value in the orchestrator domain.
/// </summary>
public sealed class EnvironmentVariableValue
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets environment key.
    /// </summary>
    public required string EnvironmentKey { get; set; }

    /// <summary>
    /// Gets or sets variable key.
    /// </summary>
    public required string VariableKey { get; set; }

    /// <summary>
    /// Gets or sets value type.
    /// </summary>
    public VariableValueType ValueType { get; set; }

    /// <summary>
    /// Gets or sets value.
    /// </summary>
    public JsonNode Value { get; set; }

    /// <summary>
    /// Gets or sets is sensitive.
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Gets or sets is resolved.
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// Gets or sets last validated on utc.
    /// </summary>
    public DateTime LastValidatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets created on utc.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets updated on utc.
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets updated by.
    /// </summary>
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets notes.
    /// </summary>
    public string Notes { get; set; }
}
