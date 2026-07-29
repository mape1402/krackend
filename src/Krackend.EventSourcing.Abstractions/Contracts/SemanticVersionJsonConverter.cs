using System.Text.Json;
using System.Text.Json.Serialization;

namespace Krackend.EventSourcing.Contracts;

/// <summary>
/// Converts semantic versions to and from their string representation.
/// </summary>
public sealed class SemanticVersionJsonConverter : JsonConverter<SemanticVersion>
{
    /// <inheritdoc />
    public override SemanticVersion Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return SemanticVersion.Parse(reader.GetString());
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        SemanticVersion value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
