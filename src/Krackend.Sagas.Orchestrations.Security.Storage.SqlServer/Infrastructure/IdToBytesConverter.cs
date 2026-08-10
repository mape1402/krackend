using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Infrastructure;

/// <summary>
/// Converts Id values to binary columns.
/// </summary>
internal sealed class IdToBytesConverter : ValueConverter<Id, byte[]>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdToBytesConverter"/> class.
    /// </summary>
    public IdToBytesConverter()
        : base(
            id => id.Value.ToByteArray(),
            bytes => new Id(new Ulid(bytes)))
    {
    }
}
