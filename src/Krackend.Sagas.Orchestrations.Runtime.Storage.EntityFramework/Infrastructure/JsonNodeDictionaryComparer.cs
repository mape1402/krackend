using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class JsonNodeDictionaryComparer : ValueComparer<Dictionary<string, JsonNode>>
{
    public JsonNodeDictionaryComparer()
        : base(
            (left, right) => AreEqual(left, right),
            value => GetStructuralHashCode(value),
            value => Clone(value))
    {
    }

    private static bool AreEqual(
        Dictionary<string, JsonNode> left,
        Dictionary<string, JsonNode> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null || left.Count != right.Count) return false;
        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue) ||
                !JsonNode.DeepEquals(pair.Value, rightValue))
                return false;
        }
        return true;
    }

    private static int GetStructuralHashCode(Dictionary<string, JsonNode> value)
    {
        if (value is null) return 0;
        // Order-independent and allocation-free. Full value equality is handled
        // by AreEqual; collisions are legal for ValueComparer hash codes.
        var hash = value.Count;
        foreach (var pair in value)
            hash ^= HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(pair.Key),
                pair.Value?.GetValueKind() ?? default);
        return hash;
    }

    private static Dictionary<string, JsonNode> Clone(Dictionary<string, JsonNode> value)
    {
        if (value is null) return new Dictionary<string, JsonNode>();
        var clone = new Dictionary<string, JsonNode>(value.Count, value.Comparer);
        foreach (var pair in value)
            clone[pair.Key] = pair.Value?.DeepClone();
        return clone;
    }
}
