using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore;

internal static class KrackendEventStoreDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseKrackendEventStoreModel(
        this DbContextOptionsBuilder optionsBuilder,
        EventStoreOptionsCollection stores)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(stores);

        var extension = new KrackendEventStoreOptionsExtension(stores);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }
}
