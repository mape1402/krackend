using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

/// <summary>
/// Converts string lists to JSON text for provider-agnostic Entity Framework storage.
/// </summary>
internal sealed class StringListJsonConverter : ValueConverter<List<string>, string>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of the <see cref="StringListJsonConverter"/> class.
    /// </summary>
    public StringListJsonConverter()
        : base(
            value => JsonSerializer.Serialize(value ?? new List<string>(), SerializerOptions),
            value => string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(value, SerializerOptions) ?? new List<string>())
    {
    }
}
