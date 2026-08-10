namespace Krackend.Sagas.Orchestrations.Security.Storage;

/// <summary>
/// Represents a paginated query result.
/// </summary>
/// <typeparam name="T">Type of row returned by the query.</typeparam>
public sealed record PagedResult<T>(
    int PageNumber,
    int TotalPages,
    long TotalRows,
    int PageSize,
    IEnumerable<T> Rows);
