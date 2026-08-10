using System.Text.Json;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonConverters;

/// <summary>
/// Represents PolymorphicJsonConverter.
/// </summary>
internal abstract class PolymorphicJsonConverter<TBase> : JsonConverter<TBase>
{
    protected abstract string DiscriminatorPropertyName { get; }
    protected abstract IReadOnlyDictionary<string, Type> TypeByDiscriminator { get; }

    private Dictionary<string, Type> _typeByDiscriminator;
    private Dictionary<Type, string> _discriminatorByType;

    /// <summary>
    /// Executes Read.
    /// </summary>
    public override TBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (!root.TryGetProperty(DiscriminatorPropertyName, out var discriminatorElement))
        {
            throw new JsonException($"Missing discriminator property '{DiscriminatorPropertyName}' for {typeof(TBase).Name}.");
        }

        var discriminator = discriminatorElement.GetString();
        if (string.IsNullOrWhiteSpace(discriminator) || !GetTypeByDiscriminator().TryGetValue(discriminator, out var concreteType))
        {
            throw new JsonException($"Unsupported discriminator '{discriminator}' for {typeof(TBase).Name}.");
        }

        var payloadJson = root.GetRawText();
        var result = JsonSerializer.Deserialize(payloadJson, concreteType, options);
        return (TBase)result;
    }

    /// <summary>
    /// Executes Write.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _discriminatorByType ??= GetTypeByDiscriminator().ToDictionary(x => x.Value, x => x.Key);

        var runtimeType = value.GetType();
        if (!_discriminatorByType.TryGetValue(runtimeType, out var discriminator))
        {
            throw new JsonException($"Unsupported runtime type '{runtimeType.Name}' for {typeof(TBase).Name}.");
        }

        var element = JsonSerializer.SerializeToElement(value, runtimeType, options);

        writer.WriteStartObject();
        writer.WriteString(DiscriminatorPropertyName, discriminator);

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, DiscriminatorPropertyName, StringComparison.Ordinal))
            {
                continue;
            }

            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private Dictionary<string, Type> GetTypeByDiscriminator()
    {
        _typeByDiscriminator ??= TypeByDiscriminator.ToDictionary(
            x => x.Key,
            x => x.Value,
            StringComparer.OrdinalIgnoreCase);

        return _typeByDiscriminator;
    }
}
