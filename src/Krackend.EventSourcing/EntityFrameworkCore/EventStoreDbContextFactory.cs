using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// Creates DbContext instances using the application's configured DbContext options.
/// </summary>
public sealed class EventStoreDbContextFactory<TDbContext> : IEventStoreDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreDbContextFactory{TDbContext}"/> class.
    /// </summary>
    public EventStoreDbContextFactory(IServiceProvider serviceProvider, DbContextOptions<TDbContext> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        ArgumentNullException.ThrowIfNull(options);
    }

    /// <inheritdoc />
    public TDbContext CreateDbContext()
        => _serviceProvider.GetRequiredService<TDbContext>();
}
