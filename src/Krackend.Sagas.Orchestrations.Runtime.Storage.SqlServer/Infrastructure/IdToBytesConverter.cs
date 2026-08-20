using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;

internal sealed class IdToBytesConverter : ValueConverter<Id, byte[]>
{
    public IdToBytesConverter()
        : base(
            id => id.Value.ToByteArray(),
            value => new Id(new Ulid(value)))
    {
    }
}
