namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents interaction paged result.
/// </summary>
public sealed record InteractionPagedResult<T>(
    int PageNumber,
    int TotalPages,
    long TotalRows,
    int PageSize,
    IEnumerable<T> Rows);


