using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class JsonNodeDictionaryConverter : ValueConverter<Dictionary<string, JsonNode>, string>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public JsonNodeDictionaryConverter()
        : base(
            value => JsonSerializer.Serialize(value ?? new Dictionary<string, JsonNode>(), SerializerOptions),
            value => string.IsNullOrWhiteSpace(value)
                ? new Dictionary<string, JsonNode>()
                : JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(value, SerializerOptions))
    {
    }
}
