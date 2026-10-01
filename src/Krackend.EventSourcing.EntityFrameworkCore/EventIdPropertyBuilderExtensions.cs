using Krackend.EventSourcing.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides Event Sourcing event id property mapping helpers.
/// </summary>
public static class EventIdPropertyBuilderExtensions
{
    /// <summary>
    /// Configures a string event id property to store ULID values as 16-byte values.
    /// </summary>
    /// <param name="propertyBuilder">The event id property builder.</param>
    /// <param name="columnType">Optional provider-specific column type, such as <c>binary(16)</c>, <c>BLOB</c>, or <c>bytea</c>.</param>
    /// <returns>The same property builder.</returns>
    public static PropertyBuilder<string> HasUlidBytesConversion(
        this PropertyBuilder<string> propertyBuilder,
        string? columnType = null)
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);

        propertyBuilder.HasConversion(new UlidEventIdToBytesConverter());

        if (!string.IsNullOrWhiteSpace(columnType))
            propertyBuilder.HasColumnType(columnType);

        return propertyBuilder;
    }
}
