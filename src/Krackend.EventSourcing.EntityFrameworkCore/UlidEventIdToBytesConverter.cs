using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// Converts ULID event id strings to compact 16-byte persisted values.
/// </summary>
public sealed class UlidEventIdToBytesConverter : ValueConverter<string, byte[]>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UlidEventIdToBytesConverter"/> class.
    /// </summary>
    public UlidEventIdToBytesConverter()
        : base(
            eventId => ToBytes(eventId),
            value => ToEventId(value),
            new ConverterMappingHints(size: 16))
    {
    }

    private static byte[] ToBytes(string eventId)
    {
        if (!Ulid.TryParse(eventId, out var ulid))
            throw new InvalidOperationException("ULID byte storage requires event identifiers to be valid ULID values.");

        return ulid.ToByteArray();
    }

    private static string ToEventId(byte[] value)
    {
        if (value.Length != 16)
            throw new InvalidOperationException("Persisted ULID event identifiers must contain exactly 16 bytes.");

        return new Ulid(value).ToString();
    }
}
