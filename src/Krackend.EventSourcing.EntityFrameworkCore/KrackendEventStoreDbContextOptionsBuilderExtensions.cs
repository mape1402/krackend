using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore;

internal static class KrackendEventStoreDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseKrackendEventStoreModel(
        this DbContextOptionsBuilder optionsBuilder,
        EventStoreOptionsCollection stores,
        Krackend.EventSourcing.EntityFrameworkCore.EntityFrameworkEventStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(options);

        var extension = new KrackendEventStoreOptionsExtension(stores, options);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }
}
