using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;

internal sealed class IdToBytesConverter : ValueConverter<Id, byte[]>
{
    public IdToBytesConverter()
        : base(id => id.Value.ToByteArray(), value => new Id(new Ulid(value)))
    {
    }
}
