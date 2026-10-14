using Krackend.EventSourcing.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides ULID property mapping helpers for Event Sourcing storage.
/// </summary>
public static class UlidPropertyBuilderExtensions
{
    /// <summary>
    /// Configures a string ULID property to store its value as 16 bytes.
    /// </summary>
    /// <param name="propertyBuilder">The ULID property builder.</param>
    /// <param name="columnType">Optional provider-specific column type, such as <c>binary(16)</c>, <c>BLOB</c>, or <c>bytea</c>.</param>
    /// <returns>The same property builder.</returns>
    public static PropertyBuilder<string> HasUlidBytesConversion(
        this PropertyBuilder<string> propertyBuilder,
        string? columnType = null)
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);

        propertyBuilder.HasConversion(new UlidStringToBytesConverter());

        if (!string.IsNullOrWhiteSpace(columnType))
            propertyBuilder.HasColumnType(columnType);

        return propertyBuilder;
    }
}
