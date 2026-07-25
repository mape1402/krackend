using System.Text.Json;

namespace Krackend.EventSourcing.Serialization;

/// <summary>
/// Serializes event payloads and metadata using System.Text.Json.
/// </summary>
public sealed class SystemTextJsonEventSerializer : IEventSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemTextJsonEventSerializer"/> class.
    /// </summary>
    public SystemTextJsonEventSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    /// <inheritdoc />
    public string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, _options);

    /// <inheritdoc />
    public object? Deserialize(string value, Type targetType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentNullException.ThrowIfNull(targetType);

        return JsonSerializer.Deserialize(value, targetType, _options);
    }
}
