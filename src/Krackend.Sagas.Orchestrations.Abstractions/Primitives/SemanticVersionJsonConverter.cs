using System.Text.Json;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

public sealed class SemanticVersionJsonConverter : JsonConverter<SemanticVersion>
{
    public override SemanticVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Semantic version value cannot be empty.");
        }

        var parts = value.Split('.');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
        {
            throw new JsonException($"Semantic version '{value}' is not valid.");
        }

        return new SemanticVersion(major, minor, patch);
    }

    public override void Write(Utf8JsonWriter writer, SemanticVersion value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
