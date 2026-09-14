namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Represents paging settings for interaction queries.
/// </summary>
public sealed class ApplicationPagedSettings
{
    /// <summary>
    /// Gets or sets page number.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets page size.
    /// </summary>
    public int PageSize { get; set; } = 25;
}
