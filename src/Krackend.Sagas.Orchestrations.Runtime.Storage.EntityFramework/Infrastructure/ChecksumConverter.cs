using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class ChecksumConverter : ValueConverter<Checksum, string>
{
    public ChecksumConverter()
        : base(
            checksum => checksum.Value,
            value => new Checksum(value))
    {
    }
}
