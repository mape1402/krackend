namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

/// <summary>
/// Represents paging, filtering, and sorting settings for list queries.
/// </summary>
public sealed record PagedSettings(
    int PageNumber,
    int PageSize,
    IEnumerable<QueryFilter> Filters,
    IEnumerable<QuerySort> Sorts);

