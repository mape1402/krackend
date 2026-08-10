namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Represents a paged interaction result.
/// </summary>
/// <typeparam name="T">Row item type.</typeparam>
public sealed record InteractionPagedResult<T>(
    int PageNumber,
    int TotalPages,
    long TotalRows,
    int PageSize,
    IEnumerable<T> Rows);
