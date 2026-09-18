using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class NullableIdToBytesConverter : ValueConverter<Id?, byte[]>
{
    public NullableIdToBytesConverter()
        : base(
            id => id.HasValue ? id.Value.Value.ToByteArray() : null,
            value => value == null ? null : new Id(new Ulid(value)),
            new ConverterMappingHints(size: 16))
    {
    }
}
