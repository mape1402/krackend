using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Converts interaction primitives to domain and storage primitives.
/// </summary>
internal static class PrimitiveParser
{
    /// <summary>
    /// Parses a string identifier into a domain Id value.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>Operation result.</returns>
    public static Id ParseId(string value)
    {
        return new Id(Ulid.Parse(value));
    }

    /// <summary>
    /// Parses a semantic version string into a domain SemanticVersion value.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>Operation result.</returns>
    public static SemanticVersion ParseSemanticVersion(string value)
    {
        string[] parts = value.Split('.');
        return new SemanticVersion(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }

    /// <summary>
    /// Formats a nullable UTC date as an ISO 8601 string.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    public static string FormatUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        return value.Value.ToString("O");
    }

    /// <summary>
    /// Maps interaction paging settings to storage paging settings.
    /// </summary>
    /// <param name="settings">Paging, filtering, and sorting settings.</param>
    /// <returns>Operation result.</returns>
    public static PagedSettings ToStoragePagedSettings(ApplicationPagedSettings settings)
    {
        return new PagedSettings(
            settings.PageNumber,
            settings.PageSize,
            settings.Filters.Select(x => new QueryFilter(x.Field, x.Operator, x.Value)),
            settings.Sorts.Select(x => new QuerySort(x.Field, x.Descending)));
    }
}


