namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents interaction paged result.
/// </summary>
public sealed record ApplicationPagedResult<T>(
    int PageNumber,
    int TotalPages,
    long TotalRows,
    int PageSize,
    IEnumerable<T> Rows);


