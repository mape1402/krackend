namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class ApplicationPagedSettings
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ApplicationPagedResult<T>
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalRows { get; set; }
    public int TotalPages { get; set; }
    public IReadOnlyCollection<T> Rows { get; set; } = Array.Empty<T>();
}
