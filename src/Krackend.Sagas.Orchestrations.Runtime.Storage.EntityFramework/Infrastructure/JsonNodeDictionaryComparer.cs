using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class JsonNodeDictionaryComparer : ValueComparer<Dictionary<string, JsonNode>>
{
    public JsonNodeDictionaryComparer()
        : base(
            (left, right) => Serialize(left) == Serialize(right),
            value => Serialize(value).GetHashCode(),
            value => Clone(value))
    {
    }

    private static string Serialize(Dictionary<string, JsonNode> value)
        => value is null ? string.Empty : System.Text.Json.JsonSerializer.Serialize(value);

    private static Dictionary<string, JsonNode> Clone(Dictionary<string, JsonNode> value)
        => value is null
            ? new Dictionary<string, JsonNode>()
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(Serialize(value));
}
