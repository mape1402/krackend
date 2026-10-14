using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// Converts ULID strings to compact 16-byte persisted values.
/// </summary>
public sealed class UlidStringToBytesConverter : ValueConverter<string, byte[]>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UlidStringToBytesConverter"/> class.
    /// </summary>
    public UlidStringToBytesConverter()
        : base(
            value => ToBytes(value),
            value => ToUlidString(value),
            new ConverterMappingHints(size: 16))
    {
    }

    private static byte[] ToBytes(string value)
    {
        if (!Ulid.TryParse(value, out var ulid))
            throw new InvalidOperationException("ULID byte storage requires valid ULID string values.");

        return ulid.ToByteArray();
    }

    private static string ToUlidString(byte[] value)
    {
        if (value.Length != 16)
            throw new InvalidOperationException("Persisted ULID values must contain exactly 16 bytes.");

        return new Ulid(value).ToString();
    }
}
