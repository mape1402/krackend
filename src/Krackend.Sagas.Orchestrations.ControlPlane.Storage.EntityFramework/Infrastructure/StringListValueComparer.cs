using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

/// <summary>
/// Compares string lists by value so JSON-converted lists are tracked correctly.
/// </summary>
internal sealed class StringListValueComparer : ValueComparer<List<string>>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of the <see cref="StringListValueComparer"/> class.
    /// </summary>
    public StringListValueComparer()
        : base(
            (left, right) => Serialize(left) == Serialize(right),
            value => Serialize(value).GetHashCode(StringComparison.Ordinal),
            value => Deserialize(Serialize(value)))
    {
    }

    private static string Serialize(List<string> value)
        => JsonSerializer.Serialize(value ?? new List<string>(), SerializerOptions);

    private static List<string> Deserialize(string value)
        => string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(value, SerializerOptions) ?? new List<string>();
}
