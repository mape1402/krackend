namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

using System.Text.Json.Nodes;

/// <summary>
/// Carries transport-agnostic metadata that enriches orchestration messages without changing the business payload.
/// </summary>
public sealed class OrchestrationPropagationMetadata
{
    /// <summary>
    /// Gets or sets the metadata items keyed by logical metadata name.
    /// </summary>
    public Dictionary<string, JsonNode> Items { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets a value indicating whether this envelope contains metadata items.
    /// </summary>
    public bool HasItems => Items is { Count: > 0 };

    /// <summary>
    /// Creates a deep clone of the current metadata envelope.
    /// </summary>
    /// <returns>A new envelope with cloned JSON values.</returns>
    public OrchestrationPropagationMetadata Clone()
    {
        var clone = new OrchestrationPropagationMetadata();
        if (Items is null)
        {
            return clone;
        }

        foreach (var item in Items)
        {
            clone.Items[item.Key] = item.Value?.DeepClone();
        }

        return clone;
    }
}
