namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents interaction paged settings.
/// </summary>
public sealed class ApplicationPagedSettings
{
    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    public int PageNumber { get; set; } = 1;
    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 25;
    /// <summary>
    /// Gets or sets the filters.
    /// </summary>
    public IEnumerable<ApplicationFilter> Filters { get; set; } = Array.Empty<ApplicationFilter>();
    /// <summary>
    /// Gets or sets the sorts.
    /// </summary>
    public IEnumerable<ApplicationSort> Sorts { get; set; } = Array.Empty<ApplicationSort>();
}


