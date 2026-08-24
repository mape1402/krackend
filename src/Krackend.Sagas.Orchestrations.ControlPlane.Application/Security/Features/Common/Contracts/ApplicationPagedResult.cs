namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Represents a paged interaction result.
/// </summary>
/// <typeparam name="T">Row item type.</typeparam>
public sealed record ApplicationPagedResult<T>(
    int PageNumber,
    int TotalPages,
    long TotalRows,
    int PageSize,
    IEnumerable<T> Rows);
