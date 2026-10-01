using Krackend.EventSourcing.Configuration;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// Configures Entity Framework Core-specific event store mapping behavior.
/// </summary>
public sealed class EntityFrameworkEventStoreOptions
{
    internal Action<PropertyBuilder<string>, EventStoreOptions>? EventIdPropertyConfigurator { get; private set; }

    /// <summary>
    /// Configures the persisted event id property for each event store entity.
    /// </summary>
    /// <param name="configure">The event id property configuration callback.</param>
    public void ConfigureEventIdProperty(Action<PropertyBuilder<string>, EventStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        EventIdPropertyConfigurator = configure;
    }
}
