using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

/// <summary>
/// Represents IdToBytesConverter.
/// </summary>
internal sealed class IdToBytesConverter : ValueConverter<Id, byte[]>
{
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public IdToBytesConverter()
        : base(
            id => id.Value.ToByteArray(),
            value => new Id(new Ulid(value)))
    {
    }
}

