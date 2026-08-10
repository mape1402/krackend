namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Represents paging settings for interaction queries.
/// </summary>
public sealed class InteractionPagedSettings
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
